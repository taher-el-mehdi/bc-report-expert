using ReportExpert.Rdl.Core;
using ReportExpert.Rdl.Core.Models;
using ReportExpert.Rdl.Core.Reading;

namespace ReportExpert.Rdl.Core.Tests;

public class DescribeReportTests
{
    [Fact]
    public void Describe_ReturnsReportStructure()
    {
        var result = ReportReader.Describe(SampleReport.Parse(), "sample.rdl");

        Assert.NotEmpty(result.DataSets);
        Assert.NotNull(result.ReportSummary);
        Assert.Equal("sample.rdl", result.FilePath);
    }

    [Fact]
    public void Describe_ListsDatasets()
    {
        var result = ReportReader.Describe(SampleReport.Parse(), "sample.rdl");

        var names = result.DataSets.Select(d => d.Name).ToList();
        Assert.Contains("MainDataset", names);
        Assert.Contains("LookupDataset", names);
    }

    [Fact]
    public void Describe_IncludesSummaryCounts()
    {
        var result = ReportReader.Describe(SampleReport.Parse(), "sample.rdl");

        Assert.Equal(2, result.ReportSummary.DataSets);
        Assert.Equal(2, result.ReportSummary.Parameters);
        Assert.Equal(3, result.ReportSummary.TableColumns);
    }

    [Fact]
    public void Describe_ReportsCommandTypeAndText()
    {
        var result = ReportReader.Describe(SampleReport.Parse(), "sample.rdl");

        var main = result.DataSets.Single(d => d.Name == "MainDataset");
        Assert.Equal("StoredProcedure", main.CommandType);
        Assert.Equal("usp_GetTestData", main.Command);
        Assert.Equal(4, main.FieldCount);
    }
}

public class GetDataSetsTests
{
    [Fact]
    public void GetDataSets_ReturnsAllDatasets()
    {
        var result = DataSetReader.GetDataSets(SampleReport.Parse());

        Assert.Equal(2, result.DataSets.Count);
    }

    [Fact]
    public void GetDataSets_IncludesFieldCount()
    {
        var result = DataSetReader.GetDataSets(SampleReport.Parse());

        Assert.Equal(4, result.DataSets.Single(d => d.Name == "MainDataset").FieldCount);
    }

    [Fact]
    public void GetDataSets_OmitsFieldsWhenLimitIsZero()
    {
        var result = DataSetReader.GetDataSets(SampleReport.Parse(), fieldLimit: 0);

        var main = result.DataSets.Single(d => d.Name == "MainDataset");
        Assert.Null(main.Fields);
        Assert.Null(main.FieldsTruncated);
    }

    [Fact]
    public void GetDataSets_WithFieldLimit_TruncatesAndFlags()
    {
        var result = DataSetReader.GetDataSets(SampleReport.Parse(), fieldLimit: 2);

        var main = result.DataSets.Single(d => d.Name == "MainDataset");
        Assert.NotNull(main.Fields);
        Assert.Equal(2, main.Fields.Count);
        Assert.True(main.FieldsTruncated);
    }

    [Fact]
    public void GetDataSets_AllFields()
    {
        var result = DataSetReader.GetDataSets(SampleReport.Parse(), fieldLimit: -1);

        var main = result.DataSets.Single(d => d.Name == "MainDataset");
        Assert.NotNull(main.Fields);
        Assert.Equal(4, main.Fields.Count);
        Assert.False(main.FieldsTruncated);
    }

    [Fact]
    public void GetDataSets_IncludesStoredProcedure()
    {
        var result = DataSetReader.GetDataSets(SampleReport.Parse());

        var main = result.DataSets.Single(d => d.Name == "MainDataset");
        Assert.Equal("StoredProcedure", main.CommandType);
        Assert.Equal("usp_GetTestData", main.CommandText);
        Assert.Equal("TestDataSource", main.DataSource);
    }

    [Fact]
    public void GetDataSets_IncludesQueryParameters()
    {
        var result = DataSetReader.GetDataSets(SampleReport.Parse());

        var main = result.DataSets.Single(d => d.Name == "MainDataset");
        var parameter = Assert.Single(main.QueryParameters);
        Assert.Equal("@StartDate", parameter.Name);
        Assert.Equal("=Parameters!StartDate.Value", parameter.Value);
    }

    [Fact]
    public void GetDataSets_IncludesFieldTypeFromDesignerNamespace()
    {
        var result = DataSetReader.GetDataSets(SampleReport.Parse(), fieldLimit: -1);

        var main = result.DataSets.Single(d => d.Name == "MainDataset");
        Assert.Equal("System.Decimal", main.Fields!.Single(f => f.Name == "Amount").Type);
    }

    [Fact]
    public void GetDataSets_FieldPatternFiltersCaseInsensitively()
    {
        var result = DataSetReader.GetDataSets(SampleReport.Parse(), fieldLimit: -1, fieldPattern: "date");

        var main = result.DataSets.Single(d => d.Name == "MainDataset");
        Assert.Equal(["CreatedDate"], main.Fields!.Select(f => f.Name));

        // The count always reflects the whole dataset so the caller can see what was filtered out.
        Assert.Equal(4, main.FieldCount);
    }

    [Fact]
    public void GetDataSets_RejectsMalformedFieldPattern()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => DataSetReader.GetDataSets(SampleReport.Parse(), fieldLimit: -1, fieldPattern: "([unclosed"));

        Assert.Equal("fieldPattern", exception.ParamName);
    }

    [Fact]
    public void GetDataSets_RejectsNegativeFieldLimitBelowMinusOne()
    {
        Assert.Throws<ArgumentException>(() => DataSetReader.GetDataSets(SampleReport.Parse(), fieldLimit: -2));
    }
}

public class GetParametersTests
{
    [Fact]
    public void GetParameters_ReturnsAllParameters()
    {
        var result = ParameterReader.GetParameters(SampleReport.Parse());

        Assert.Equal(2, result.Parameters.Count);
    }

    [Fact]
    public void GetParameters_IncludesTypeAndPrompt()
    {
        var result = ParameterReader.GetParameters(SampleReport.Parse());

        var start = result.Parameters.Single(p => p.Name == "StartDate");
        Assert.Equal("DateTime", start.DataType);
        Assert.Equal("Start Date", start.Prompt);
    }

    [Fact]
    public void GetParameters_OmitsAbsentOptionalSections()
    {
        var result = ParameterReader.GetParameters(SampleReport.Parse());

        var start = result.Parameters.Single(p => p.Name == "StartDate");
        Assert.Null(start.DefaultValues);
        Assert.Null(start.ValidValues);
        Assert.Null(start.ValidValuesDataSet);
    }
}

public class GetColumnsTests
{
    [Fact]
    public void GetColumns_ReturnsColumnInfo()
    {
        var result = ColumnReader.GetColumns(SampleReport.Parse());

        Assert.Equal(3, result.Columns.Count);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GetColumns_IncludesHeaders()
    {
        var result = ColumnReader.GetColumns(SampleReport.Parse());

        var headers = result.Columns.Select(c => c.Header).ToList();
        Assert.Contains("ID", headers);
        Assert.Contains("Name", headers);
        Assert.Contains("Amount", headers);
    }

    [Fact]
    public void GetColumns_IncludesFieldBinding()
    {
        var result = ColumnReader.GetColumns(SampleReport.Parse());

        var amount = result.Columns.Single(c => c.Header == "Amount");
        Assert.Equal("=Fields!Amount.Value", amount.FieldBinding);
        Assert.Equal("Amount", amount.FieldName);
    }

    [Fact]
    public void GetColumns_IncludesWidthsAndFormat()
    {
        var result = ColumnReader.GetColumns(SampleReport.Parse());

        Assert.Equal(["1in", "2in", "1.5in"], result.Columns.Select(c => c.Width));
        Assert.Equal("#,0.00", result.Columns.Single(c => c.Header == "Amount").Format);
    }

    [Fact]
    public void GetColumns_IncludesHeaderTextboxNames()
    {
        var result = ColumnReader.GetColumns(SampleReport.Parse());

        Assert.Equal(["HeaderID", "HeaderName", "HeaderAmount"], result.Columns.Select(c => c.TextboxName));
    }

    [Fact]
    public void GetColumns_ReportsMissingTablixInPayload()
    {
        var document = RdlDocument.Parse($"""
            <?xml version="1.0" encoding="utf-8"?>
            <Report xmlns="{SampleReport.Namespace}">
              <DataSets />
            </Report>
            """);

        var result = ColumnReader.GetColumns(document);

        Assert.Empty(result.Columns);
        Assert.Equal("No Tablix found", result.Error);
    }

    [Fact]
    public void GetColumns_ThrowsWhenNamedTablixIsMissing()
    {
        var exception = Assert.Throws<RdlNotFoundException>(
            () => ColumnReader.GetColumns(SampleReport.Parse(), "NoSuchTablix"));

        Assert.Equal(RdlTarget.Tablix, exception.Target);
        Assert.Contains("MainTable", exception.Message, StringComparison.Ordinal);
    }
}

public class LayoutReadingTests
{
    [Fact]
    public void GetTablixes_DescribesTheGrid()
    {
        var result = ReportReader.GetTablixes(SampleReport.Parse());

        var tablix = Assert.Single(result.Tablixes);
        Assert.Equal(0, tablix.Index);
        Assert.Equal("MainTable", tablix.Name);
        Assert.Equal("MainDataset", tablix.DataSetName);
        Assert.Equal(3, tablix.ColumnCount);
        Assert.Equal(2, tablix.RowCount);
    }

    [Fact]
    public void GetPageSetup_DerivesOrientationAndUsableWidth()
    {
        var result = ReportReader.GetPageSetup(SampleReport.Parse());

        Assert.Equal("8.5in", result.PageWidth);
        Assert.Equal("11in", result.PageHeight);
        Assert.Equal("Portrait", result.Orientation);
        Assert.Equal("7.50in", result.UsableWidth);
        Assert.Equal(1, result.Columns);
    }

    [Fact]
    public void GetReportItems_FindsTextboxesAndTheTablix()
    {
        var result = ReportReader.GetReportItems(SampleReport.Parse());

        Assert.Contains(result.Items, i => i is { Kind: "Tablix", Name: "MainTable" });
        Assert.Contains(result.Items, i => i is { Kind: "Image", Name: "CompanyLogo" });
        Assert.Equal(6, result.Items.Count(i => i.Kind == "Textbox"));
        Assert.Equal(result.Count, result.Items.Count);
    }

    [Fact]
    public void GetReportItems_FiltersByKind()
    {
        var result = ReportReader.GetReportItems(SampleReport.Parse(), "Textbox");

        Assert.All(result.Items, item => Assert.Equal("Textbox", item.Kind));
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void GetReportItems_ReportsTextboxValueAndPath()
    {
        var result = ReportReader.GetReportItems(SampleReport.Parse(), "Textbox");

        var header = result.Items.Single(i => i.Name == "HeaderAmount");
        Assert.Equal("Amount", header.Value);
        Assert.Contains("MainTable", header.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void GetReportItems_RejectsUnknownKind()
    {
        Assert.Throws<ArgumentException>(() => ReportReader.GetReportItems(SampleReport.Parse(), "Sprocket"));
    }
}
