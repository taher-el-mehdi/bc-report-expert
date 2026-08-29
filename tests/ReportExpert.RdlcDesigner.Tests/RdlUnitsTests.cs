using ReportExpert.RdlcDesigner.Model;

namespace ReportExpert.RdlcDesigner.Tests;

public class RdlUnitsTests
{
    [Theory]
    [InlineData("1in", 96)]
    [InlineData("0.5in", 48)]
    [InlineData("72pt", 96)]
    public void ToPx_ConvertsKnownUnits(string raw, double expected) =>
        Assert.Equal(expected, RdlUnits.ToPx(raw), precision: 3);

    [Fact]
    public void FromPx_RoundTripsInches()
    {
        double px = RdlUnits.ToPx("1.25in");
        string raw = RdlUnits.FromPx(px, "1.25in");
        Assert.Equal(px, RdlUnits.ToPx(raw), precision: 3);
    }
}
