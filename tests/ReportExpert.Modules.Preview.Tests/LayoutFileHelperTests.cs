using ReportExpert.Modules.Preview.Helpers;
using ReportExpert.Modules.Preview.Models;

namespace ReportExpert.Modules.Preview.Tests;

public class LayoutFileHelperTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("report.rdlc", true)]
    [InlineData("REPORT.RDLC", true)]
    [InlineData(@"C:\layouts\invoice.xlsx", true)]
    [InlineData("letter.docx", true)]
    [InlineData("notes.txt", false)]
    public void IsSupportedLayout_MatchesKnownExtensions(string? path, bool expected)
    {
        Assert.Equal(expected, LayoutFileHelper.IsSupportedLayout(path));
    }

    [Theory]
    [InlineData("a.rdlc", LayoutKind.Rdlc)]
    [InlineData("a.xlsx", LayoutKind.Excel)]
    [InlineData("a.docx", LayoutKind.Word)]
    [InlineData("a.pdf", LayoutKind.None)]
    public void DetectKind_MapsExtension(string path, LayoutKind expected)
    {
        Assert.Equal(expected, LayoutFileHelper.DetectKind(path));
    }

    [Fact]
    public void MatchRank_PrefersRdlcOverOfficeLayouts()
    {
        Assert.True(LayoutFileHelper.MatchRank("a.rdlc") < LayoutFileHelper.MatchRank("a.xlsx"));
        Assert.True(LayoutFileHelper.MatchRank("a.xlsx") < LayoutFileHelper.MatchRank("a.docx"));
        Assert.True(LayoutFileHelper.MatchRank("a.docx") < LayoutFileHelper.MatchRank("a.bin"));
    }
}
