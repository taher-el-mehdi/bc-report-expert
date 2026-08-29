using ReportExpert.Modules.Copilot.Models;
using ReportExpert.Modules.Copilot.Services;

namespace ReportExpert.Modules.Copilot.Tests;

public class RdlcPatchParserTests
{
    [Fact]
    public void Parse_JsonPatchBlock_ReadsFindAndReplace()
    {
        const string response = """
            Here is a fix:
            ```rdlc-patch
            {"description":"Widen header","find":"<Width>2in</Width>","replace":"<Width>3in</Width>"}
            ```
            """;

        var patches = RdlcPatchParser.Parse(response);

        RdlcPatch patch = Assert.Single(patches);
        Assert.Equal("Widen header", patch.Description);
        Assert.Equal("<Width>2in</Width>", patch.Find);
        Assert.Equal("<Width>3in</Width>", patch.Replace);
        Assert.False(patch.IsFullDocument);
    }

    [Fact]
    public void Parse_BeforeAfterBlock_ReadsFragments()
    {
        const string response = """
            ```rdlc-patch
            <before><Hidden>false</Hidden></before>
            <after><Hidden>true</Hidden></after>
            ```
            """;

        var patches = RdlcPatchParser.Parse(response);

        RdlcPatch patch = Assert.Single(patches);
        Assert.Equal("<Hidden>false</Hidden>", patch.Find);
        Assert.Equal("<Hidden>true</Hidden>", patch.Replace);
    }

    [Fact]
    public void Parse_BareXmlReport_TreatsAsFullDocumentReplace()
    {
        const string response = """
            ```xml
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition">
              <Body />
            </Report>
            ```
            """;

        var patches = RdlcPatchParser.Parse(response);

        RdlcPatch patch = Assert.Single(patches);
        Assert.True(patch.IsFullDocument);
        Assert.Contains("<Report", patch.Replace, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPatch_PartialReplace_SwapsFragment()
    {
        const string xml = "<Report><Width>2in</Width></Report>";
        var patch = new RdlcPatch { Find = "<Width>2in</Width>", Replace = "<Width>3in</Width>" };

        string result = RdlcPatchParser.ApplyPatch(xml, patch);

        Assert.Equal("<Report><Width>3in</Width></Report>", result);
    }

    [Fact]
    public void ApplyPatch_MissingFind_Throws()
    {
        var patch = new RdlcPatch { Find = "<Width>9in</Width>", Replace = "<Width>3in</Width>" };

        Assert.Throws<InvalidOperationException>(() =>
            RdlcPatchParser.ApplyPatch("<Report><Width>2in</Width></Report>", patch));
    }
}
