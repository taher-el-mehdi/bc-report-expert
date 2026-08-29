using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ReportExpert.Rdl.Core;
using ReportExpert.Rdl.Core.Models;
using ReportExpert.Rdl.Core.Validation;

namespace ReportExpert.Rdl.Mcp.Server;

/// <summary>
/// The single place every tool goes through to touch a report definition.
/// </summary>
/// <remarks>
/// <para>
/// Concentrating path validation, locking, backups, re-validation, the atomic commit and exception
/// mapping here means a new tool is a few lines of domain logic and cannot forget any of it.
/// </para>
/// <para>
/// The write sequence is: lock, load, edit in memory, re-validate, back up, write atomically.
/// Nothing touches the original file until the edit is known to be good, and the write itself is a
/// rename, so an interrupted call leaves the report exactly as it was.
/// </para>
/// </remarks>
public static class ToolGuards
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathLocks = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] AllowedExtensions = [".rdl", ".rdlc"];

    /// <summary>
    /// Runs a read-only operation against a report definition.
    /// </summary>
    /// <typeparam name="T">The payload type the operation produces.</typeparam>
    /// <param name="filePath">Path to the report definition.</param>
    /// <param name="read">The operation.</param>
    /// <returns>The wrapped result, or a failure envelope.</returns>
    public static ToolResponse Read<T>(string filePath, Func<RdlDocument, T> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        return Execute(() =>
        {
            string resolved = ResolvePath(filePath);
            using var scope = Lock(resolved);

            return ToolResponse.Success(read(RdlDocument.Load(resolved)));
        });
    }

    /// <summary>
    /// Runs an operation that does not open a report definition, such as listing backups.
    /// </summary>
    /// <typeparam name="T">The payload type the operation produces.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <returns>The wrapped result, or a failure envelope.</returns>
    public static ToolResponse Run<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return Execute(() => ToolResponse.Success(operation()));
    }

    /// <summary>
    /// Applies an edit to a report definition.
    /// </summary>
    /// <param name="filePath">Path to the report definition.</param>
    /// <param name="dryRun">
    /// When <see langword="true"/> the edit is performed in memory and returned as a unified diff
    /// without writing anything to disk.
    /// </param>
    /// <param name="edit">The edit to apply.</param>
    /// <returns>
    /// A success envelope whose payload carries <c>success</c>, <c>message</c> and, for a real
    /// write, <c>backup_path</c>. Or a failure envelope.
    /// </returns>
    public static ToolResponse Mutate(string filePath, bool dryRun, Func<RdlDocument, EditOutcome> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        return Execute(() =>
        {
            string resolved = ResolvePath(filePath);
            using var scope = Lock(resolved);

            var document = RdlDocument.Load(resolved);

            // Captured before the edit so a report that was already broken is not blamed on it.
            bool wasValid = RdlValidator.Validate(document).Valid;
            string before = dryRun ? document.Serialize() : string.Empty;

            var outcome = edit(document);

            if (wasValid)
            {
                var after = RdlValidator.Validate(document);
                if (!after.Valid)
                {
                    return ToolResponse.Failure(
                        ToolErrorCodes.ValidationFailed,
                        $"The edit was not applied because it would have made the report invalid: " +
                        $"{string.Join("; ", after.Issues ?? [])}",
                        "Fix the underlying problem first, or call validate_rdl to see the full picture. " +
                        "Nothing was written, so the report on disk is unchanged.");
                }
            }

            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["success"] = true,
                ["message"] = outcome.Message,
                ["dry_run"] = dryRun,
            };

            if (outcome.Details is not null)
            {
                foreach ((string key, object? value) in outcome.Details)
                    payload[key] = value;
            }

            if (dryRun)
            {
                payload["diff"] = UnifiedDiff.Create(before, document.Serialize(), Path.GetFileName(resolved));
                payload["message"] = $"[dry run, nothing written] {outcome.Message}";
                return ToolResponse.Success(payload);
            }

            if (ServerOptions.BackupsEnabled)
                payload["backup_path"] = BackupStore.Create(resolved);

            document.SaveAs(resolved);

            return ToolResponse.Success(payload);
        });
    }

    /// <summary>
    /// Runs an operation that replaces a report definition wholesale, such as restoring a backup.
    /// </summary>
    /// <param name="filePath">Path to the report definition.</param>
    /// <param name="operation">The operation, returning the message and details to report.</param>
    /// <returns>The wrapped result, or a failure envelope.</returns>
    public static ToolResponse Replace(string filePath, Func<string, EditOutcome> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return Execute(() =>
        {
            string resolved = ResolvePath(filePath, mustExist: false);
            using var scope = Lock(resolved);

            var outcome = operation(resolved);

            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["success"] = true,
                ["message"] = outcome.Message,
            };

            if (outcome.Details is not null)
            {
                foreach ((string key, object? value) in outcome.Details)
                    payload[key] = value;
            }

            return ToolResponse.Success(payload);
        });
    }

    /// <summary>
    /// Turns a path supplied by a caller into a full path this server is willing to open.
    /// </summary>
    /// <param name="filePath">The caller-supplied path.</param>
    /// <param name="mustExist">Whether the file has to exist already.</param>
    /// <returns>The resolved absolute path.</returns>
    /// <exception cref="ArgumentException">The path is unusable or outside the allowed roots.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist and was required to.</exception>
    internal static string ResolvePath(string filePath, bool mustExist = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A report definition path is required.", nameof(filePath));

        string resolved;
        try
        {
            resolved = Path.GetFullPath(filePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException($"'{filePath}' is not a usable file path: {ex.Message}", nameof(filePath), ex);
        }

        string extension = Path.GetExtension(resolved);
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"'{Path.GetFileName(resolved)}' is not a report definition. Expected a .rdl or .rdlc file.",
                nameof(filePath));
        }

        if (ServerOptions.AllowedRoots.Count > 0 && !IsUnderAllowedRoot(resolved))
        {
            throw new ArgumentException(
                $"'{resolved}' is outside the directories this server is allowed to access.",
                nameof(filePath));
        }

        if (mustExist && !File.Exists(resolved))
            throw new FileNotFoundException($"No report definition found at '{resolved}'.", resolved);

        return resolved;
    }

    private static bool IsUnderAllowedRoot(string resolved)
    {
        foreach (string root in ServerOptions.AllowedRoots)
        {
            if (resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Serializes access to one report definition, both within this process and against any other
    /// process running this server.
    /// </summary>
    private static LockScope Lock(string resolvedPath) => new(resolvedPath);

    private sealed class LockScope : IDisposable
    {
        private readonly SemaphoreSlim _local;
        private readonly Mutex? _global;
        private readonly bool _globalHeld;

        public LockScope(string resolvedPath)
        {
            _local = PathLocks.GetOrAdd(resolvedPath, static _ => new SemaphoreSlim(1, 1));
            _local.Wait();

            try
            {
                _global = new Mutex(initiallyOwned: false, MutexName(resolvedPath));

                try
                {
                    // A stale mutex from a crashed peer still grants ownership; the file itself is
                    // always re-read under the lock, so inheriting an abandoned one is safe.
                    _globalHeld = _global.WaitOne(TimeSpan.FromSeconds(30));
                }
                catch (AbandonedMutexException)
                {
                    _globalHeld = true;
                }

                if (!_globalHeld)
                {
                    throw new IOException(
                        $"Timed out waiting for exclusive access to '{Path.GetFileName(resolvedPath)}'. " +
                        "Another process is holding it.");
                }
            }
            catch
            {
                _global?.Dispose();
                _local.Release();
                throw;
            }
        }

        public void Dispose()
        {
            if (_global is not null)
            {
                if (_globalHeld)
                    _global.ReleaseMutex();

                _global.Dispose();
            }

            _local.Release();
        }

        /// <summary>
        /// Derives a legal, collision-resistant mutex name from a file path, which may contain
        /// separators and exceed the name length limit.
        /// </summary>
        private static string MutexName(string resolvedPath)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(resolvedPath.ToLowerInvariant()));
            return "Local\\rdlc-mcp-" + Convert.ToHexString(hash)[..32];
        }
    }

    /// <summary>
    /// Runs an operation and turns any exception into a failure envelope with an actionable hint.
    /// </summary>
    /// <remarks>
    /// A thrown exception would surface to the model as an opaque protocol error, so every escape
    /// route is mapped here. The catch-all is deliberate: a crashed stdio server is far worse for
    /// the caller than a described failure.
    /// </remarks>
    private static ToolResponse Execute(Func<ToolResponse> operation)
    {
        try
        {
            return operation();
        }
        catch (FileNotFoundException ex)
        {
            return ToolResponse.Failure(
                ToolErrorCodes.FileNotFound,
                ex.Message,
                "Check the path. It must point at an existing .rdl or .rdlc file and may be absolute or relative to this server's working directory.");
        }
        catch (DirectoryNotFoundException ex)
        {
            return ToolResponse.Failure(
                ToolErrorCodes.FileNotFound,
                ex.Message,
                "Check that every directory in the path exists.");
        }
        catch (RdlNotFoundException ex)
        {
            return ToolResponse.Failure(
                ToolErrorCodes.NotFound,
                ex.Message,
                HintFor(ex.Target));
        }
        catch (RdlRefusedException ex)
        {
            return ToolResponse.Failure(ToolErrorCodes.Refused, ex.Message, ex.Hint);
        }
        catch (InvalidRdlException ex)
        {
            return ToolResponse.Failure(
                ToolErrorCodes.InvalidRdl,
                ex.Message,
                "Open the file and confirm it is a valid RDL or RDLC report definition. Nothing was written.");
        }
        catch (ResourceLimitExceededException ex)
        {
            return ToolResponse.Failure(
                ToolErrorCodes.ResourceLimit,
                ex.Message,
                "This report is too large for the server's safety limits. Split it, or raise the limits and rebuild.");
        }
        catch (ArgumentException ex)
        {
            string code = ex.ParamName == "filePath" ? ToolErrorCodes.InvalidPath : ToolErrorCodes.InvalidArgument;

            return ToolResponse.Failure(
                code,
                ex.Message,
                $"Correct the '{WireName(ex.ParamName)}' argument and call the tool again. Nothing was changed.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Failure(
                ToolErrorCodes.FileLocked,
                ex.Message,
                "The file is read-only or this process lacks permission. Clear the read-only flag and retry.");
        }
        catch (IOException ex)
        {
            return ToolResponse.Failure(
                ToolErrorCodes.FileLocked,
                ex.Message,
                "The file is in use by another program. Close it in Visual Studio or the report designer and retry.");
        }
        catch (Exception ex)
        {
            return ToolResponse.Failure(
                ToolErrorCodes.Internal,
                $"{ex.GetType().Name}: {ex.Message}",
                "This is a bug in the RDLC MCP server. The report was not modified. Report the message above.");
        }
    }

    /// <summary>
    /// Translates a .NET parameter name into the name the caller actually sent.
    /// </summary>
    /// <remarks>
    /// The domain layer names its parameters in camelCase, but the tool surface is snake_case.
    /// Naming <c>columnIndex</c> in a hint would point the caller at an argument that does not
    /// exist in the schema they were given.
    /// </remarks>
    private static string WireName(string? parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
            return "argument";

        // The path parameter is spelled without a separator on the wire, so it cannot be derived.
        if (string.Equals(parameterName, "filePath", StringComparison.Ordinal))
            return "filepath";

        var builder = new StringBuilder(parameterName.Length + 4);

        foreach (char character in parameterName)
        {
            if (char.IsUpper(character))
            {
                if (builder.Length > 0)
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string HintFor(RdlTarget target) => target switch
    {
        RdlTarget.DataSet => "Call get_rdl_datasets to see the datasets this report defines.",
        RdlTarget.Field => "Call get_rdl_datasets with field_limit=-1 to see the fields this dataset declares.",
        RdlTarget.Parameter => "Call get_rdl_parameters to see the parameters this report defines.",
        RdlTarget.Tablix => "Call get_rdl_tablixes to see the tablixes in this report.",
        RdlTarget.Column => "Call get_rdl_columns to see the columns and their indexes.",
        RdlTarget.Row => "Call get_rdl_columns to see how this tablix is laid out.",
        RdlTarget.Textbox => "Call get_rdl_report_items with kind=\"Textbox\" to see the textboxes in this report.",
        RdlTarget.ReportItem => "Call get_rdl_report_items to see the layout items in this report.",
        RdlTarget.Page => "This report has no page setup section, so page geometry cannot be read or changed.",
        RdlTarget.Backup => "Call list_rdl_backups to see the backups available for this report.",
        _ => "Call describe_rdl_report to see the structure of this report.",
    };
}
