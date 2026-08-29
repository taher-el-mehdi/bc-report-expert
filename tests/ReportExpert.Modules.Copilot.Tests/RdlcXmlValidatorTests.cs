using ReportExpert.Modules.Copilot.Services;

namespace ReportExpert.Modules.Copilot.Tests;

public class RdlcXmlValidatorTests
{
    [Fact]
    public void Validate_Empty_IsInvalid()
    {
        var (ok, message) = RdlcXmlValidator.Validate("  ");
        Assert.False(ok);
        Assert.Contains("No XML", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_MalformedXml_IsInvalid()
    {
        var (ok, message) = RdlcXmlValidator.Validate("<Report>");
        Assert.False(ok);
        Assert.Contains("Invalid XML", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_MissingBody_IsInvalid()
    {
        var (ok, message) = RdlcXmlValidator.Validate("<Report><Page /></Report>");
        Assert.False(ok);
        Assert.Contains("Body", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DuplicateNames_IsInvalid()
    {
        const string xml = """
            <Report>
              <Body>
                <Textbox Name="Title" />
                <Textbox Name="Title" />
              </Body>
            </Report>
            """;

        var (ok, message) = RdlcXmlValidator.Validate(xml);
        Assert.False(ok);
        Assert.Contains("Duplicate", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Title", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WellFormedWithBody_IsValid()
    {
        const string xml = """
            <Report>
              <Body>
                <Textbox Name="Title" />
              </Body>
            </Report>
            """;

        var (ok, message) = RdlcXmlValidator.Validate(xml);
        Assert.True(ok);
        Assert.Contains("well-formed", message, StringComparison.OrdinalIgnoreCase);
    }
}
