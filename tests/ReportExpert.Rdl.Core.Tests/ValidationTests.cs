using ReportExpert.Rdl.Core;
using ReportExpert.Rdl.Core.Validation;

namespace ReportExpert.Rdl.Core.Tests;

public class ValidationTests
{
    [Fact]
    public void ValidReport_Passes()
    {
        var result = RdlValidator.Validate(SampleReport.Parse());

        Assert.True(result.Valid);
        Assert.Null(result.Issues);
        Assert.Equal("RDL structure is valid", result.Message);
    }

    [Fact]
    public void InvalidFieldReference_Fails()
    {
        string xml = SampleReport.Xml.Replace(
            "=Fields!Amount.Value",
            "=Fields!NonExistent.Value",
            StringComparison.Ordinal);

        var result = RdlValidator.Validate(RdlDocument.Parse(xml));

        Assert.False(result.Valid);
        Assert.NotNull(result.Issues);
        Assert.Contains(result.Issues, issue => issue.Contains("NonExistent", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidFieldReference_ListsAvailableFields()
    {
        string xml = SampleReport.Xml.Replace(
            "=Fields!Amount.Value",
            "=Fields!NonExistent.Value",
            StringComparison.Ordinal);

        var result = RdlValidator.Validate(RdlDocument.Parse(xml));

        string issue = Assert.Single(result.Issues!);
        Assert.Contains("Available fields:", issue, StringComparison.Ordinal);
        Assert.Contains("Amount", issue, StringComparison.Ordinal);
        // The location names the textbox rather than an XML path.
        Assert.Contains("DataAmount", issue, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingDatasets_Fails()
    {
        var document = RdlDocument.Parse($"""
            <?xml version="1.0" encoding="utf-8"?>
            <Report xmlns="{SampleReport.Namespace}">
              <DataSets></DataSets>
              <ReportSections><ReportSection><Body><ReportItems>
              </ReportItems></Body></ReportSection></ReportSections>
            </Report>
            """);

        var result = RdlValidator.Validate(document);

        Assert.False(result.Valid);
        Assert.Contains(result.Issues!, issue => issue.Contains("No datasets", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingTablix_Fails()
    {
        var document = RdlDocument.Parse($"""
            <?xml version="1.0" encoding="utf-8"?>
            <Report xmlns="{SampleReport.Namespace}">
              <DataSets>
                <DataSet Name="Only"><Query><CommandText>x</CommandText></Query></DataSet>
              </DataSets>
            </Report>
            """);

        var result = RdlValidator.Validate(document);

        Assert.False(result.Valid);
        Assert.Contains(result.Issues!, issue => issue.Contains("No Tablix", StringComparison.Ordinal));
    }

    [Fact]
    public void TablixWithoutDataSetName_Warns_ButStaysValid()
    {
        string xml = SampleReport.Xml.Replace(
            "<DataSetName>MainDataset</DataSetName>",
            string.Empty,
            StringComparison.Ordinal);

        var result = RdlValidator.Validate(RdlDocument.Parse(xml));

        Assert.True(result.Valid);
        Assert.NotNull(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Contains("no DataSetName", StringComparison.Ordinal));
    }

    [Fact]
    public void TablixBoundToUnknownDataset_Fails()
    {
        string xml = SampleReport.Xml.Replace(
            "<DataSetName>MainDataset</DataSetName>",
            "<DataSetName>Ghost</DataSetName>",
            StringComparison.Ordinal);

        var result = RdlValidator.Validate(RdlDocument.Parse(xml));

        Assert.False(result.Valid);
        Assert.Contains(result.Issues!, issue =>
            issue.Contains("unknown dataset", StringComparison.Ordinal) &&
            issue.Contains("Ghost", StringComparison.Ordinal));
    }

    [Fact]
    public void CrossDatasetLookup_IsNotReportedAsBroken()
    {
        string xml = SampleReport.Xml.Replace(
            "=Fields!Name.Value",
            "=Lookup(Fields!ID.Value, Fields!LookupID.Value, Fields!LookupValue.Value, \"LookupDataset\")",
            StringComparison.Ordinal);

        var result = RdlValidator.Validate(RdlDocument.Parse(xml));

        Assert.True(result.Valid);
    }

    [Fact]
    public void RepeatedBrokenReference_IsReportedOnce()
    {
        // The same missing field appears in both the header and the detail row.
        string xml = SampleReport.Xml
            .Replace("<Value>Amount</Value>", "<Value>=Fields!Ghost.Value</Value>", StringComparison.Ordinal)
            .Replace("=Fields!Amount.Value", "=Fields!Ghost.Value", StringComparison.Ordinal);

        var result = RdlValidator.Validate(RdlDocument.Parse(xml));

        Assert.False(result.Valid);
        Assert.Single(result.Issues!);
    }
}

public class ExpressionParserTests
{
    [Fact]
    public void SimpleFieldReference()
    {
        var result = ExpressionParser.ExtractWithContext("=Fields!Name.Value", "MainDataset");

        Assert.Contains("Name", result["MainDataset"]);
    }

    [Fact]
    public void AggregateFunction()
    {
        var result = ExpressionParser.ExtractWithContext("=Sum(Fields!Amount.Value)", "MainDataset");

        Assert.Contains("Amount", result["MainDataset"]);
    }

    [Fact]
    public void AggregateWithScope_AttributesToTheNamedDataset()
    {
        var result = ExpressionParser.ExtractWithContext(
            "=First(Fields!LookupValue.Value, \"LookupDataset\")",
            "MainDataset");

        Assert.Contains("LookupValue", result["LookupDataset"]);
        Assert.False(result.ContainsKey("MainDataset"));
    }

    [Fact]
    public void LookupFunction_SplitsArgumentsAcrossDatasets()
    {
        var result = ExpressionParser.ExtractWithContext(
            "=Lookup(Fields!ID.Value, Fields!LookupID.Value, Fields!LookupValue.Value, \"LookupDataset\")",
            "MainDataset");

        Assert.Contains("ID", result["MainDataset"]);
        Assert.Contains("LookupID", result["LookupDataset"]);
        Assert.Contains("LookupValue", result["LookupDataset"]);
    }

    [Fact]
    public void ComplexExpression_AttributesEverythingToTheCurrentScope()
    {
        var result = ExpressionParser.ExtractWithContext(
            "=IIF(Fields!Amount.Value > 0, Fields!Name.Value, \"N/A\")",
            "MainDataset");

        Assert.Contains("Amount", result["MainDataset"]);
        Assert.Contains("Name", result["MainDataset"]);
    }

    [Fact]
    public void NonExpression_ReturnsEmpty()
    {
        var result = ExpressionParser.ExtractWithContext("Static Text", "MainDataset");

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankExpression_ReturnsEmpty(string? expression)
    {
        Assert.Empty(ExpressionParser.ExtractWithContext(expression, "MainDataset"));
        Assert.Empty(ExpressionParser.Extract(expression));
    }

    [Fact]
    public void RepeatedField_IsListedOnce()
    {
        var result = ExpressionParser.ExtractWithContext(
            "=Fields!Amount.Value + Fields!Amount.Value",
            "MainDataset");

        Assert.Equal(["Amount"], result["MainDataset"]);
    }

    [Fact]
    public void Extract_IgnoresDatasetScope()
    {
        var result = ExpressionParser.Extract(
            "=Lookup(Fields!ID.Value, Fields!LookupID.Value, Fields!LookupValue.Value, \"LookupDataset\")");

        Assert.Equal(["ID", "LookupID", "LookupValue"], result);
    }

    [Fact]
    public void LookupWinsOverAggregate_ForTheSameReference()
    {
        // First() would otherwise re-claim LookupValue for the default dataset.
        var result = ExpressionParser.ExtractWithContext(
            "=Lookup(Fields!ID.Value, Fields!LookupID.Value, First(Fields!LookupValue.Value), \"LookupDataset\")",
            "MainDataset");

        Assert.Contains("LookupValue", result["LookupDataset"]);
        Assert.DoesNotContain("LookupValue", result["MainDataset"]);
    }
}

public class FieldUsageTests
{
    [Fact]
    public void FindFieldUsages_LocatesTheDetailBinding()
    {
        var result = RdlValidator.FindFieldUsages(SampleReport.Parse(), "Amount");

        Assert.Equal("Amount", result.FieldName);
        var usage = Assert.Single(result.Usages);
        Assert.Equal("MainDataset", usage.DataSet);
        Assert.Equal("DataAmount", usage.Location);
    }

    [Fact]
    public void FindFieldUsages_ReturnsNothingForAnUnusedField()
    {
        var result = RdlValidator.FindFieldUsages(SampleReport.Parse(), "CreatedDate");

        Assert.Equal(0, result.Count);
        Assert.Empty(result.Usages);
    }

    [Fact]
    public void FindFieldUsages_FiltersByDataset()
    {
        var result = RdlValidator.FindFieldUsages(SampleReport.Parse(), "Amount", "LookupDataset");

        Assert.Empty(result.Usages);
    }
}
