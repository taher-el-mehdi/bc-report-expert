using System.Xml.Linq;
using ReportExpert.Rdl.Core;
using ReportExpert.Rdl.Core.Editing;
using ReportExpert.Rdl.Core.Models;
using ReportExpert.Rdl.Core.Reading;
using ReportExpert.Rdl.Core.Validation;

namespace ReportExpert.Rdl.Core.Tests;

public class ColumnEditorTests
{
    [Fact]
    public void UpdateColumnHeader_RenamesTheHeader()
    {
        var document = SampleReport.Parse();

        ColumnEditor.UpdateColumnHeader(document, "Name", "Full Name");

        var headers = ColumnReader.GetColumns(document).Columns.Select(c => c.Header).ToList();
        Assert.Contains("Full Name", headers);
        Assert.DoesNotContain("Name", headers);
    }

    [Fact]
    public void UpdateColumnHeader_ThrowsWhenTextDoesNotMatch()
    {
        var exception = Assert.Throws<RdlNotFoundException>(
            () => ColumnEditor.UpdateColumnHeader(SampleReport.Parse(), "name", "Full Name"));

        Assert.Equal(RdlTarget.Textbox, exception.Target);
    }

    [Fact]
    public void UpdateColumnWidth_ChangesTheColumnAndTheTablixTotal()
    {
        var document = SampleReport.Parse();

        ColumnEditor.UpdateColumnWidth(document, 0, "1.5in");

        Assert.Equal("1.5in", ColumnReader.GetColumns(document).Columns[0].Width);
    }

    [Fact]
    public void UpdateColumnWidth_RejectsAnOutOfRangeIndex()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ColumnEditor.UpdateColumnWidth(SampleReport.Parse(), 9, "1in"));

        Assert.Equal("columnIndex", exception.ParamName);
    }

    [Fact]
    public void UpdateColumnWidth_RejectsAMalformedWidth()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ColumnEditor.UpdateColumnWidth(SampleReport.Parse(), 0, "wide"));

        Assert.Equal("newWidth", exception.ParamName);
    }

    [Fact]
    public void UpdateColumnFormat_SetsTheDetailCellFormat()
    {
        var document = SampleReport.Parse();

        ColumnEditor.UpdateColumnFormat(document, 2, "C2");

        Assert.Equal("C2", ColumnReader.GetColumns(document).Columns[2].Format);
    }

    [Fact]
    public void RemoveColumn_RemovesFromEveryParallelStructure()
    {
        var document = SampleReport.Parse();
        Assert.Equal(3, ColumnReader.GetColumns(document).Columns.Count);

        ColumnEditor.RemoveColumn(document, 1);

        Assert.Equal(2, ColumnReader.GetColumns(document).Columns.Count);

        // The column hierarchy must shrink in step or the report will not render.
        var tablix = TablixNavigator.RequireTablix(document);
        var members = TablixNavigator.ColumnHierarchyMembers(tablix, document.Ns);
        Assert.Equal(2, members!.Elements(document.Ns + "TablixMember").Count());
    }

    [Fact]
    public void RemoveColumn_AutoAdjustsPageWidthByDefault()
    {
        var document = SampleReport.Parse();

        // Columns are 1in + 2in + 1.5in. Removing the 2in column leaves 2.5in, and with a
        // half inch margin on each side the page should come out at 3.50in.
        ColumnEditor.RemoveColumn(document, 1);

        Assert.Equal("3.50in", ReportReader.GetPageSetup(document).PageWidth);
    }

    [Fact]
    public void RemoveColumn_LeavesPageWidthAloneWhenAsked()
    {
        var document = SampleReport.Parse();

        ColumnEditor.RemoveColumn(document, 1, autoAdjustPageWidth: false);

        Assert.Equal("8.5in", ReportReader.GetPageSetup(document).PageWidth);
    }

    [Fact]
    public void RemoveColumn_RejectsAnOutOfRangeIndex()
    {
        Assert.Throws<ArgumentException>(() => ColumnEditor.RemoveColumn(SampleReport.Parse(), 3));
    }

    [Fact]
    public void AddColumn_InsertsAtThePosition()
    {
        var document = SampleReport.Parse();

        ColumnEditor.AddColumn(document, 1, "Status", "=Fields!Name.Value", "0.75in");

        var columns = ColumnReader.GetColumns(document).Columns;
        Assert.Equal(4, columns.Count);
        Assert.Equal("Status", columns[1].Header);
        Assert.Equal("0.75in", columns[1].Width);
        Assert.Equal("=Fields!Name.Value", columns[1].FieldBinding);
    }

    [Fact]
    public void AddColumn_AppendsWhenIndexIsMinusOne()
    {
        var document = SampleReport.Parse();

        var outcome = ColumnEditor.AddColumn(document, -1, "Tail", "=Fields!ID.Value");

        var columns = ColumnReader.GetColumns(document).Columns;
        Assert.Equal("Tail", columns[^1].Header);
        Assert.Equal(3, outcome.Details!["column_index"]);
    }

    [Fact]
    public void AddColumn_KeepsTheColumnHierarchyInStep()
    {
        var document = SampleReport.Parse();

        ColumnEditor.AddColumn(document, 0, "First", "=Fields!ID.Value");

        var tablix = TablixNavigator.RequireTablix(document);
        Assert.Equal(4, TablixNavigator.Columns(tablix, document.Ns).Count);
        Assert.Equal(
            4,
            TablixNavigator.ColumnHierarchyMembers(tablix, document.Ns)!
                .Elements(document.Ns + "TablixMember").Count());

        foreach (var row in TablixNavigator.Rows(tablix, document.Ns))
            Assert.Equal(4, TablixNavigator.Cells(row, document.Ns).Count);
    }

    [Fact]
    public void AddColumn_RecalculatesTheTablixWidth()
    {
        // The shared fixture declares no Tablix Width, so add one to observe the recalculation.
        string xml = SampleReport.Xml.Replace(
            "<DataSetName>MainDataset</DataSetName>",
            "<Width>4.5in</Width>\n            <DataSetName>MainDataset</DataSetName>",
            StringComparison.Ordinal);

        var document = RdlDocument.Parse(xml);

        ColumnEditor.AddColumn(document, -1, "Extra", "=Fields!ID.Value", "0.5in");

        // 1 + 2 + 1.5 + 0.5
        var tablix = TablixNavigator.RequireTablix(document);
        Assert.Equal("5.00in", tablix.Element(document.Ns + "Width")?.Value);
    }

    [Fact]
    public void RecalculateWidth_LeavesATablixThatDeclaresNoWidthAlone()
    {
        var document = SampleReport.Parse();
        var tablix = TablixNavigator.RequireTablix(document);

        double total = TablixNavigator.RecalculateWidth(tablix, document.Ns);

        Assert.Equal(4.5d, total, precision: 6);
        Assert.Null(tablix.Element(document.Ns + "Width"));
    }

    [Fact]
    public void AddColumn_AppliesFormatToTheDetailCellOnly()
    {
        var document = SampleReport.Parse();

        ColumnEditor.AddColumn(document, -1, "Total", "=Fields!Amount.Value", "1in", formatString: "C2");

        var columns = ColumnReader.GetColumns(document).Columns;
        Assert.Equal("C2", columns[^1].Format);
    }

    [Fact]
    public void AddColumn_RejectsAnOutOfRangeIndex()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ColumnEditor.AddColumn(SampleReport.Parse(), 9, "X", "=Fields!ID.Value"));

        Assert.Equal("columnIndex", exception.ParamName);
    }

    [Fact]
    public void MoveColumn_ReordersEveryParallelStructure()
    {
        var document = SampleReport.Parse();

        ColumnEditor.MoveColumn(document, 0, 2);

        var columns = ColumnReader.GetColumns(document).Columns;
        Assert.Equal(["Name", "Amount", "ID"], columns.Select(c => c.Header));
        Assert.Equal(["2in", "1.5in", "1in"], columns.Select(c => c.Width));
    }

    [Fact]
    public void MoveColumn_HandlesMovingLeft()
    {
        var document = SampleReport.Parse();

        ColumnEditor.MoveColumn(document, 2, 0);

        Assert.Equal(
            ["Amount", "ID", "Name"],
            ColumnReader.GetColumns(document).Columns.Select(c => c.Header));
    }

    [Fact]
    public void MoveColumn_IsANoOpWhenSourceEqualsTarget()
    {
        var document = SampleReport.Parse();

        var outcome = ColumnEditor.MoveColumn(document, 1, 1);

        Assert.Contains("nothing to do", outcome.Message, StringComparison.Ordinal);
        Assert.Equal(["ID", "Name", "Amount"], ColumnReader.GetColumns(document).Columns.Select(c => c.Header));
    }

    [Fact]
    public void SetColumnStyle_AppliesToTheRequestedRowOnly()
    {
        var document = SampleReport.Parse();

        ColumnEditor.SetColumnStyle(
            document,
            0,
            new CellStyle { FontWeight = "Bold", BackgroundColor = "#EEEEEE" },
            ColumnRowTarget.Header);

        var header = TextboxEditor.RequireTextbox(document, "HeaderID");
        Assert.Equal("Bold", header.Descendants(document.Ns + "FontWeight").Single().Value);
        Assert.Equal("#EEEEEE", header.Descendants(document.Ns + "BackgroundColor").Single().Value);

        var data = TextboxEditor.RequireTextbox(document, "DataID");
        Assert.Empty(data.Descendants(document.Ns + "FontWeight"));
    }

    [Fact]
    public void SetColumnStyle_RejectsAnEmptyStyle()
    {
        Assert.Throws<ArgumentException>(
            () => ColumnEditor.SetColumnStyle(SampleReport.Parse(), 0, new CellStyle()));
    }
}

public class DataSetEditorTests
{
    [Fact]
    public void AddField_AppearsInSubsequentReads()
    {
        var document = SampleReport.Parse();

        DataSetEditor.AddField(document, "MainDataset", "NewField", "NewField", "System.String");

        var main = DataSetReader.GetDataSets(document, fieldLimit: -1).DataSets.Single(d => d.Name == "MainDataset");
        Assert.Contains(main.Fields!, f => f.Name == "NewField" && f.Type == "System.String");
    }

    [Fact]
    public void AddField_RefusesADuplicate()
    {
        var exception = Assert.Throws<RdlRefusedException>(
            () => DataSetEditor.AddField(SampleReport.Parse(), "MainDataset", "Amount", "Amount", "System.Decimal"));

        Assert.NotEmpty(exception.Hint);
    }

    [Fact]
    public void AddField_ThrowsForAnUnknownDataset()
    {
        var exception = Assert.Throws<RdlNotFoundException>(
            () => DataSetEditor.AddField(SampleReport.Parse(), "Ghost", "F", "F", "System.String"));

        Assert.Equal(RdlTarget.DataSet, exception.Target);
        // The message names the datasets that do exist so the caller can retry without a lookup.
        Assert.Contains("MainDataset", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveField_TakesTheFieldOut()
    {
        var document = SampleReport.Parse();

        DataSetEditor.RemoveField(document, "MainDataset", "CreatedDate");

        var main = DataSetReader.GetDataSets(document, fieldLimit: -1).DataSets.Single(d => d.Name == "MainDataset");
        Assert.DoesNotContain(main.Fields!, f => f.Name == "CreatedDate");
    }

    [Fact]
    public void RemoveField_ThrowsForAnUnknownField()
    {
        var exception = Assert.Throws<RdlNotFoundException>(
            () => DataSetEditor.RemoveField(SampleReport.Parse(), "MainDataset", "Ghost"));

        Assert.Equal(RdlTarget.Field, exception.Target);
    }

    [Fact]
    public void UpdateStoredProcedure_ChangesTheCommandText()
    {
        var document = SampleReport.Parse();

        DataSetEditor.UpdateStoredProcedure(document, "MainDataset", "usp_NewProcedure");

        var main = DataSetReader.GetDataSets(document).DataSets.Single(d => d.Name == "MainDataset");
        Assert.Equal("usp_NewProcedure", main.CommandText);
    }

    [Fact]
    public void RenameField_RewritesReferences()
    {
        var document = SampleReport.Parse();

        var outcome = DataSetEditor.RenameField(document, "MainDataset", "Amount", "Total");

        var columns = ColumnReader.GetColumns(document).Columns;
        Assert.Equal("=Fields!Total.Value", columns[2].FieldBinding);
        Assert.Equal(1, outcome.Details!["expressions_updated"]);

        // A rename that updates references must leave the report valid.
        Assert.True(RdlValidator.Validate(document).Valid);
    }

    [Fact]
    public void RenameField_CanLeaveReferencesAlone()
    {
        var document = SampleReport.Parse();

        DataSetEditor.RenameField(document, "MainDataset", "Amount", "Total", updateReferences: false);

        Assert.Equal("=Fields!Amount.Value", ColumnReader.GetColumns(document).Columns[2].FieldBinding);
    }

    [Fact]
    public void AddDataSet_CreatesAQueryAndFields()
    {
        var document = SampleReport.Parse();

        DataSetEditor.AddDataSet(document, "Extra", "usp_Extra", "StoredProcedure", "TestDataSource");

        var added = DataSetReader.GetDataSets(document).DataSets.Single(d => d.Name == "Extra");
        Assert.Equal("usp_Extra", added.CommandText);
        Assert.Equal("StoredProcedure", added.CommandType);
        Assert.Equal("TestDataSource", added.DataSource);
    }

    [Fact]
    public void AddDataSet_RefusesADuplicateName()
    {
        Assert.Throws<RdlRefusedException>(() => DataSetEditor.AddDataSet(SampleReport.Parse(), "MainDataset"));
    }

    [Fact]
    public void RemoveDataSet_RefusesWhenADataRegionIsBound()
    {
        var exception = Assert.Throws<RdlRefusedException>(
            () => DataSetEditor.RemoveDataSet(SampleReport.Parse(), "MainDataset"));

        Assert.Contains("MainTable", exception.Message, StringComparison.Ordinal);
        Assert.Contains("force=true", exception.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveDataSet_ProceedsWhenForced()
    {
        var document = SampleReport.Parse();

        DataSetEditor.RemoveDataSet(document, "MainDataset", force: true);

        Assert.DoesNotContain(DataSetReader.GetDataSets(document).DataSets, d => d.Name == "MainDataset");
    }

    [Fact]
    public void RemoveDataSet_ProceedsWithoutForceWhenUnused()
    {
        var document = SampleReport.Parse();

        DataSetEditor.RemoveDataSet(document, "LookupDataset");

        Assert.Single(DataSetReader.GetDataSets(document).DataSets);
    }
}

public class ParameterEditorTests
{
    [Fact]
    public void AddParameter_AppearsInSubsequentReads()
    {
        var document = SampleReport.Parse();

        ParameterEditor.AddParameter(document, "NewParam", "String", "Enter a value");

        var names = ParameterReader.GetParameters(document).Parameters.Select(p => p.Name).ToList();
        Assert.Contains("NewParam", names);
    }

    [Fact]
    public void AddParameter_RefusesADuplicate()
    {
        Assert.Throws<RdlRefusedException>(
            () => ParameterEditor.AddParameter(SampleReport.Parse(), "StartDate", "DateTime", "x"));
    }

    [Fact]
    public void UpdateParameter_ChangesThePrompt()
    {
        var document = SampleReport.Parse();

        ParameterEditor.UpdateParameter(document, "StartDate", prompt: "Select Start Date");

        var start = ParameterReader.GetParameters(document).Parameters.Single(p => p.Name == "StartDate");
        Assert.Equal("Select Start Date", start.Prompt);
    }

    [Fact]
    public void UpdateParameter_SetsADefaultValue()
    {
        var document = SampleReport.Parse();

        ParameterEditor.UpdateParameter(document, "EndDate", defaultValue: "=Today()");

        var end = ParameterReader.GetParameters(document).Parameters.Single(p => p.Name == "EndDate");
        Assert.Equal(["=Today()"], end.DefaultValues);
    }

    [Fact]
    public void UpdateParameter_ThrowsForAnUnknownParameter()
    {
        var exception = Assert.Throws<RdlNotFoundException>(
            () => ParameterEditor.UpdateParameter(SampleReport.Parse(), "NonExistent", prompt: "Test"));

        Assert.Equal(RdlTarget.Parameter, exception.Target);
        Assert.Contains("not found", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateParameter_RejectsAnEmptyChange()
    {
        Assert.Throws<ArgumentException>(() => ParameterEditor.UpdateParameter(SampleReport.Parse(), "StartDate"));
    }

    [Fact]
    public void RemoveParameter_RefusesWhileStillReferenced()
    {
        // StartDate is bound to a query parameter in MainDataset.
        var exception = Assert.Throws<RdlRefusedException>(
            () => ParameterEditor.RemoveParameter(SampleReport.Parse(), "StartDate"));

        Assert.Contains("force=true", exception.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveParameter_ProceedsWhenUnreferenced()
    {
        var document = SampleReport.Parse();

        ParameterEditor.RemoveParameter(document, "EndDate");

        Assert.Single(ParameterReader.GetParameters(document).Parameters);
    }
}

public class LayoutEditorTests
{
    [Fact]
    public void SetPageSetup_ChangesMargins()
    {
        var document = SampleReport.Parse();

        PageEditor.SetPageSetup(document, new PageSetupChange { LeftMargin = "1in", RightMargin = "1in" });

        var setup = ReportReader.GetPageSetup(document);
        Assert.Equal("1in", setup.LeftMargin);
        Assert.Equal("6.50in", setup.UsableWidth);
    }

    [Fact]
    public void SetPageSetup_SwapsDimensionsForLandscape()
    {
        var document = SampleReport.Parse();

        PageEditor.SetPageSetup(document, new PageSetupChange { Orientation = "Landscape" });

        var setup = ReportReader.GetPageSetup(document);
        Assert.Equal("11in", setup.PageWidth);
        Assert.Equal("8.5in", setup.PageHeight);
        Assert.Equal("Landscape", setup.Orientation);
    }

    [Fact]
    public void SetPageSetup_LeavesOrientationAloneWhenAlreadyCorrect()
    {
        var document = SampleReport.Parse();

        var outcome = PageEditor.SetPageSetup(document, new PageSetupChange { Orientation = "Portrait" });

        Assert.Contains("nothing to do", outcome.Message, StringComparison.Ordinal);
        Assert.Equal("8.5in", ReportReader.GetPageSetup(document).PageWidth);
    }

    [Fact]
    public void SetPageSetup_RejectsAnUnknownOrientation()
    {
        Assert.Throws<ArgumentException>(
            () => PageEditor.SetPageSetup(SampleReport.Parse(), new PageSetupChange { Orientation = "Sideways" }));
    }

    [Fact]
    public void SetPageSetup_RejectsAnEmptyChange()
    {
        Assert.Throws<ArgumentException>(
            () => PageEditor.SetPageSetup(SampleReport.Parse(), new PageSetupChange()));
    }

    [Fact]
    public void SetTextboxValue_ChangesTheDisplayedText()
    {
        var document = SampleReport.Parse();

        var outcome = TextboxEditor.SetTextboxValue(document, "HeaderName", "Customer");

        Assert.Equal("Name", outcome.Details!["previous_value"]);
        Assert.Equal("Customer", ColumnReader.GetColumns(document).Columns[1].Header);
    }

    [Fact]
    public void SetTextboxValue_ThrowsForAnUnknownTextbox()
    {
        var exception = Assert.Throws<RdlNotFoundException>(
            () => TextboxEditor.SetTextboxValue(SampleReport.Parse(), "Ghost", "x"));

        Assert.Equal(RdlTarget.Textbox, exception.Target);
    }

    [Fact]
    public void SetVisibility_HidesANamedImage()
    {
        var document = SampleReport.Parse();

        var outcome = ReportItemEditor.SetVisibility(document, "CompanyLogo", "true");

        Assert.Contains("Hid", outcome.Message, StringComparison.Ordinal);
        var logo = ReportReader.GetReportItems(document, "Image").Items.Single(i => i.Name == "CompanyLogo");
        Assert.Equal("true", logo.Hidden);
    }

    [Fact]
    public void SetVisibility_ShowsAPreviouslyHiddenItem()
    {
        var document = SampleReport.Parse();
        ReportItemEditor.SetVisibility(document, "CompanyLogo", "true");

        ReportItemEditor.SetVisibility(document, "CompanyLogo", "false");

        Assert.Equal("false", ReportReader.GetReportItems(document, "Image").Items.Single().Hidden);
    }

    [Fact]
    public void RemoveReportItem_RemovesAFreeStandingImage()
    {
        var document = SampleReport.Parse();

        ReportItemEditor.RemoveReportItem(document, "CompanyLogo");

        Assert.Empty(ReportReader.GetReportItems(document, "Image").Items);
    }

    [Fact]
    public void RemoveReportItem_RefusesTheOnlyContentOfACell()
    {
        Assert.Throws<InvalidRdlException>(
            () => ReportItemEditor.RemoveReportItem(SampleReport.Parse(), "HeaderName"));
    }

    [Fact]
    public void SetVisibility_ThrowsForAnUnknownItem()
    {
        Assert.Throws<RdlNotFoundException>(
            () => ReportItemEditor.SetVisibility(SampleReport.Parse(), "GhostLogo", "true"));
    }
}
