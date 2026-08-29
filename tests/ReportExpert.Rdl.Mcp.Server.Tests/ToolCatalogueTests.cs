using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using ReportExpert.Rdl.Mcp.Server;

namespace ReportExpert.Rdl.Mcp.Server.Tests;

/// <summary>
/// Rules that every tool must satisfy, enforced by reflection so a new tool cannot skip them.
/// </summary>
public class ToolCatalogueTests
{
    private static readonly string[] PortedToolNames =
    [
        "describe_rdl_report",
        "get_rdl_datasets",
        "get_rdl_parameters",
        "get_rdl_columns",
        "validate_rdl",
        "update_column_header",
        "update_column_width",
        "update_column_format",
        "add_column",
        "remove_column",
        "update_stored_procedure",
        "add_dataset_field",
        "remove_dataset_field",
        "add_parameter",
        "update_parameter",
    ];

    public static TheoryData<string> ToolNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var tool in Tools())
                data.Add(NameOf(tool));

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void EveryToolNameIsSnakeCase(string name)
    {
        Assert.Matches("^[a-z][a-z0-9_]*$", name);
    }

    [Fact]
    public void EveryPortedToolIsStillPresent()
    {
        var present = Tools().Select(NameOf).ToHashSet(StringComparer.Ordinal);

        var missing = PortedToolNames.Where(name => !present.Contains(name)).ToList();
        Assert.True(missing.Count == 0, $"These tools were dropped: {string.Join(", ", missing)}");
    }

    [Fact]
    public void ToolNamesAreUnique()
    {
        var duplicates = Tools()
            .GroupBy(NameOf, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate tool names: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void EveryToolHasADescription()
    {
        var undescribed = Tools()
            .Where(tool => string.IsNullOrWhiteSpace(tool.GetCustomAttribute<DescriptionAttribute>()?.Description))
            .Select(NameOf)
            .ToList();

        Assert.True(undescribed.Count == 0, $"These tools have no description: {string.Join(", ", undescribed)}");
    }

    [Fact]
    public void EveryToolParameterHasADescription()
    {
        // The parameter descriptions are the only guidance a model gets about how to fill in a
        // call, so an undocumented parameter is a tool the model will get wrong.
        var undescribed =
            from tool in Tools()
            from parameter in tool.GetParameters()
            where string.IsNullOrWhiteSpace(parameter.GetCustomAttribute<DescriptionAttribute>()?.Description)
            select $"{NameOf(tool)}.{parameter.Name}";

        var missing = undescribed.ToList();
        Assert.True(missing.Count == 0, $"These parameters have no description: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryToolTakesAFilepathFirst()
    {
        var wrong = Tools()
            .Where(tool => tool.GetParameters().FirstOrDefault()?.Name != "filepath")
            .Select(NameOf)
            .ToList();

        Assert.True(wrong.Count == 0, $"These tools do not start with a filepath parameter: {string.Join(", ", wrong)}");
    }

    [Fact]
    public void EveryToolReturnsTheEnvelope()
    {
        var wrong = Tools()
            .Where(tool => tool.ReturnType != typeof(ToolResponse))
            .Select(NameOf)
            .ToList();

        Assert.True(wrong.Count == 0, $"These tools do not return ToolResponse: {string.Join(", ", wrong)}");
    }

    [Fact]
    public void EveryMutatingToolOffersADryRun()
    {
        // restore_rdl_backup is the exception: it replaces the file wholesale from a copy the
        // caller can already inspect with list_rdl_backups, so a diff would add nothing.
        string[] exempt = ["restore_rdl_backup"];

        var missing = Tools()
            .Where(tool => IsMutating(tool) && !exempt.Contains(NameOf(tool), StringComparer.Ordinal))
            .Where(tool => !tool.GetParameters().Any(p => p.Name == "dry_run"))
            .Select(NameOf)
            .ToList();

        Assert.True(missing.Count == 0, $"These mutating tools have no dry_run: {string.Join(", ", missing)}");
    }

    [Fact]
    public void ReadOnlyToolsAreMarkedAsSuch()
    {
        // The ReadOnly hint is what lets a host auto-approve a call without asking the user.
        var unmarked = Tools()
            .Where(tool => NameOf(tool).StartsWith("get_", StringComparison.Ordinal)
                        || NameOf(tool).StartsWith("describe_", StringComparison.Ordinal)
                        || NameOf(tool).StartsWith("find_", StringComparison.Ordinal)
                        || NameOf(tool).StartsWith("list_", StringComparison.Ordinal)
                        || NameOf(tool) == "validate_rdl")
            .Where(tool => tool.GetCustomAttribute<McpServerToolAttribute>()?.ReadOnly != true)
            .Select(NameOf)
            .ToList();

        Assert.True(unmarked.Count == 0, $"These read-only tools are not marked ReadOnly: {string.Join(", ", unmarked)}");
    }

    [Fact]
    public void EveryToolIsDocumentedInTheReadme()
    {
        var documented = DocumentedToolNames();

        var missing = Tools()
            .Select(NameOf)
            .Where(name => !documented.Contains(name))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"These tools are missing from the server README tool tables: {string.Join(", ", missing)}");
    }

    [Fact]
    public void TheReadmeDoesNotDocumentToolsThatNoLongerExist()
    {
        var present = Tools().Select(NameOf).ToHashSet(StringComparer.Ordinal);

        var stale = DocumentedToolNames().Where(name => !present.Contains(name)).ToList();

        Assert.True(
            stale.Count == 0,
            $"The README documents tools that do not exist: {string.Join(", ", stale)}");
    }

    [Fact]
    public void TheReadmeCountsTheToolsCorrectly()
    {
        // The tables are checked row by row above, but the sentence introducing them carries three
        // totals that nothing else would catch drifting.
        var tools = Tools().ToList();
        int readOnly = tools.Count(tool => !IsMutating(tool));

        var stated = System.Text.RegularExpressions.Regex.Match(
            EmbeddedText("server-readme.md"),
            @"(?<total>[\w-]+) tools, grouped by what they touch\. (?<read>[\w-]+) are read-only.*?the other (?<write>[\w-]+) write",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        Assert.True(stated.Success, "The README no longer states how many tools there are.");

        Assert.Equal(tools.Count, Spelled(stated.Groups["total"].Value));
        Assert.Equal(readOnly, Spelled(stated.Groups["read"].Value));
        Assert.Equal(tools.Count - readOnly, Spelled(stated.Groups["write"].Value));
    }

    /// <summary>Reads a number the README spells out in words.</summary>
    private static int Spelled(string word)
    {
        string[] units =
        [
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
            "eighteen", "nineteen",
        ];

        var parts = word.ToLowerInvariant().Split('-', StringSplitOptions.RemoveEmptyEntries);

        int total = 0;

        foreach (var part in parts)
        {
            int unit = Array.IndexOf(units, part);

            total += part switch
            {
                "twenty" => 20,
                "thirty" => 30,
                _ when unit >= 0 => unit,
                _ => throw new InvalidOperationException($"The README spells a number this test cannot read: '{word}'."),
            };
        }

        return total;
    }

    /// <summary>
    /// Reads the tool names out of the README's tool tables, where each row opens with the name in
    /// backticks. Scanning the tables rather than the whole document keeps prose mentions from
    /// counting as documentation.
    /// </summary>
    private static HashSet<string> DocumentedToolNames() =>
        System.Text.RegularExpressions.Regex
            .Matches(EmbeddedText("server-readme.md"), @"^\| *`([a-z][a-z0-9_]*)` *\|", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static bool IsMutating(MethodInfo tool)
    {
        var attribute = tool.GetCustomAttribute<McpServerToolAttribute>();
        return attribute?.ReadOnly != true;
    }

    private static string NameOf(MethodInfo tool) =>
        tool.GetCustomAttribute<McpServerToolAttribute>()?.Name ?? tool.Name;

    private static IEnumerable<MethodInfo> Tools() =>
        typeof(ToolResponse).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null);

    private static string EmbeddedText(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The '{resourceName}' resource is missing from the test assembly.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
