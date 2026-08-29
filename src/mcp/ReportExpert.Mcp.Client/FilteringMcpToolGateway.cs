namespace ReportExpert.Mcp.Client;

/// <summary>
/// Wraps a gateway so the user can turn individual tools off without restarting the servers.
/// </summary>
/// <remarks>
/// Disabled tools are omitted from <see cref="ListToolsAsync"/> so the model is not offered them,
/// and <see cref="CallToolAsync"/> refuses them if the model still asks — for example from an
/// earlier turn that listed the tool while it was still enabled.
/// </remarks>
public sealed class FilteringMcpToolGateway : IMcpToolGateway, IFilteredMcpToolGateway
{
    private readonly IMcpToolGateway _inner;
    private readonly Func<string, bool> _isEnabled;

    /// <summary>Creates a filter over an existing gateway.</summary>
    /// <param name="inner">The unfiltered gateway.</param>
    /// <param name="isEnabled">
    /// Returns <see langword="true"/> when the qualified tool name may be offered and called.
    /// </param>
    public FilteringMcpToolGateway(IMcpToolGateway inner, Func<string, bool> isEnabled)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(isEnabled);

        _inner = inner;
        _isEnabled = isEnabled;
    }

    /// <inheritdoc />
    public bool IsToolEnabled(string qualifiedName) =>
        string.IsNullOrEmpty(qualifiedName) || _isEnabled(qualifiedName);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<McpToolDescriptor>> ListAllToolsAsync(
        CancellationToken cancellationToken = default) =>
        _inner.ListToolsAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await _inner.ListToolsAsync(cancellationToken).ConfigureAwait(false);
        return [.. all.Where(tool => _isEnabled(tool.QualifiedName))];
    }

    /// <inheritdoc />
    public ValueTask<McpToolResult> CallToolAsync(
        string qualifiedName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled(qualifiedName))
        {
            string toolName = McpToolRegistry.TrySplit(qualifiedName, out _, out string bare)
                ? bare
                : qualifiedName;

            return ValueTask.FromResult(new McpToolResult(
                IsError: true,
                Text: DisabledToolMessage(toolName)));
        }

        return _inner.CallToolAsync(qualifiedName, arguments, cancellationToken);
    }

    /// <summary>The message returned to the model (and shown on the tool card) when a tool is off.</summary>
    public static string DisabledToolMessage(string toolName) =>
        $"You have to enable the tool '{toolName}' under Settings → Agent mode → Tool servers " +
        "before this action can be performed. Tell the user this clearly and do not retry until they enable it.";
}
