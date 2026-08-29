using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Views;

public partial class ExpressionEditorWindow : Window
{
    public ExpressionEditorWindow(
        string? initialExpression,
        IReadOnlyList<RdlcDataSetNode> dataSets,
        IReadOnlyList<RdlcParameterNode> parameters)
    {
        InitializeComponent();
        ExpressionBox.Text = initialExpression ?? string.Empty;
        BuildTree(dataSets, parameters);
        Loaded += (_, _) =>
        {
            ExpressionBox.Focus();
            ExpressionBox.CaretIndex = ExpressionBox.Text.Length;
        };
    }

    public string Expression => ExpressionBox.Text;

    private void BuildTree(
        IReadOnlyList<RdlcDataSetNode> dataSets,
        IReadOnlyList<RdlcParameterNode> parameters)
    {
        var fieldsRoot = new TreeViewItem { Header = "Fields", IsExpanded = true };
        foreach (RdlcDataSetNode ds in dataSets)
        {
            var dsNode = new TreeViewItem { Header = ds.Name, IsExpanded = true };
            foreach (RdlcFieldNode field in ds.Fields)
            {
                dsNode.Items.Add(new TreeViewItem
                {
                    Header = field.Name,
                    Tag = $"=Fields!{field.Name}.Value"
                });
            }

            fieldsRoot.Items.Add(dsNode);
        }

        var paramsRoot = new TreeViewItem { Header = "Parameters", IsExpanded = true };
        foreach (RdlcParameterNode p in parameters)
        {
            paramsRoot.Items.Add(new TreeViewItem
            {
                Header = p.Name,
                Tag = $"=Parameters!{p.Name}.Value"
            });
        }

        var globalsRoot = new TreeViewItem { Header = "Globals", IsExpanded = true };
        foreach (string g in new[] { "PageNumber", "TotalPages", "ExecutionTime", "ReportName" })
        {
            globalsRoot.Items.Add(new TreeViewItem
            {
                Header = g,
                Tag = $"=Globals!{g}"
            });
        }

        CategoryTree.Items.Add(fieldsRoot);
        CategoryTree.Items.Add(paramsRoot);
        CategoryTree.Items.Add(globalsRoot);
    }

    private void CategoryTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CategoryTree.SelectedItem is TreeViewItem { Tag: string expr })
            InsertText(expr);
    }

    private void InsertFields_Click(object sender, RoutedEventArgs e) => InsertText("=Fields!FieldName.Value");
    private void InsertParams_Click(object sender, RoutedEventArgs e) => InsertText("=Parameters!ParamName.Value");
    private void InsertGlobals_Click(object sender, RoutedEventArgs e) => InsertText("=Globals!PageNumber");

    private void InsertText(string text)
    {
        int caret = ExpressionBox.CaretIndex;
        string current = ExpressionBox.Text ?? string.Empty;
        ExpressionBox.Text = current.Insert(Math.Clamp(caret, 0, current.Length), text);
        ExpressionBox.CaretIndex = caret + text.Length;
        ExpressionBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
