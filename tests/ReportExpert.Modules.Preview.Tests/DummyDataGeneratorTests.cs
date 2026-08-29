using System.Data;
using ReportExpert.Modules.Preview.Generators;
using ReportExpert.Modules.Preview.Models;

namespace ReportExpert.Modules.Preview.Tests;

public class DummyDataGeneratorTests
{
    private readonly DummyDataGenerator _sut = new();

    [Fact]
    public void GenerateTable_HonorsRowCountAndColumnNames()
    {
        var dataset = new RdlcDataSetInfo
        {
            Name = "Header",
            Fields =
            [
                new RdlcFieldInfo { Name = "No", DataField = "No", ClrType = typeof(string), TypeName = "System.String" },
                new RdlcFieldInfo { Name = "Amount", DataField = "Amount", ClrType = typeof(decimal), TypeName = "System.Decimal" }
            ]
        };

        DataTable table = _sut.GenerateTable(dataset, rowCount: 3);

        Assert.Equal("Header", table.TableName);
        Assert.Equal(3, table.Rows.Count);
        Assert.True(table.Columns.Contains("No"));
        Assert.True(table.Columns.Contains("Amount"));
        Assert.Equal(typeof(decimal), table.Columns["Amount"]!.DataType);
    }

    [Fact]
    public void GenerateValue_EmailField_UsesExampleDomain()
    {
        var field = new RdlcFieldInfo
        {
            Name = "Email",
            DataField = "Email",
            ClrType = typeof(string),
            TypeName = "System.String"
        };

        object? value = _sut.GenerateValue(field, rowIndex: 0);

        string text = Assert.IsType<string>(value);
        Assert.Contains("@example.com", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateValue_ShowTotalFlag_EmitsTrueFalseStrings()
    {
        var field = new RdlcFieldInfo
        {
            Name = "ShowTotal",
            DataField = "ShowTotal",
            ClrType = typeof(string),
            TypeName = "System.String"
        };

        Assert.Equal("True", _sut.GenerateValue(field, 0));
        Assert.Equal("False", _sut.GenerateValue(field, 1));
    }

    [Fact]
    public void BuildSchemaText_ListsFields()
    {
        var dataset = new RdlcDataSetInfo
        {
            Name = "Lines",
            Fields = [new RdlcFieldInfo { Name = "Qty", TypeName = "System.Int32" }]
        };

        string schema = DummyDataGenerator.BuildSchemaText(dataset);

        Assert.Contains("Dataset: Lines", schema, StringComparison.Ordinal);
        Assert.Contains("Qty (System.Int32)", schema, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportTableToCsv_EscapesCommasAndQuotes()
    {
        var table = new DataTable("T");
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add("Smith, \"Jr.\"");

        string csv = DummyDataGenerator.ExportTableToCsv(table);

        Assert.Contains("\"Smith, \"\"Jr.\"\"\"", csv, StringComparison.Ordinal);
    }
}
