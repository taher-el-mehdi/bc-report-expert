using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Model;

namespace ReportExpert.RdlcDesigner.Services;

public sealed class PropertyService : IPropertyService
{
    private readonly Func<RdlcDocument?> _document;
    private readonly IUndoService _undo;
    private readonly Action? _afterChange;

    public PropertyService(Func<RdlcDocument?> document, IUndoService undo, Action? afterChange = null)
    {
        _document = document;
        _undo = undo;
        _afterChange = afterChange;
    }

    public void SetName(IReportItem item, string name)
    {
        if (item is not ReportItemModel model || !model.CanEditName)
            return;
        if (string.Equals(model.Name, name, StringComparison.Ordinal))
            return;

        _undo.Execute(new SetNameCommand(model, model.Name, name, After));
    }

    public void SetValue(IReportItem item, string? value)
    {
        if (item is not ReportItemModel model || !model.CanEditValue)
            return;
        if (string.Equals(model.Value, value, StringComparison.Ordinal))
            return;

        _undo.Execute(new SetValueCommand(model, model.Value, value, After));
    }

    public void SetGeometry(IReportItem item, double leftPx, double topPx, double widthPx, double heightPx)
    {
        if (item is not ReportItemModel model || !model.CanEditGeometry)
            return;

        if (NearlyEqual(model.LeftPx, leftPx) && NearlyEqual(model.TopPx, topPx) &&
            NearlyEqual(model.WidthPx, widthPx) && NearlyEqual(model.HeightPx, heightPx))
            return;

        _undo.Execute(new SetGeometryCommand(
            model,
            model.LeftPx, model.TopPx, model.WidthPx, model.HeightPx,
            leftPx, topPx, widthPx, heightPx,
            After));
    }

    public void SetFont(IReportItem item, string? family, string? size, string? weight, string? color)
    {
        if (item is not ReportItemModel model || !model.CanEditStyle)
            return;

        if (string.Equals(model.FontFamily, family, StringComparison.Ordinal) &&
            string.Equals(model.FontSize, size, StringComparison.Ordinal) &&
            string.Equals(model.FontWeight, weight, StringComparison.Ordinal) &&
            string.Equals(model.Color, color, StringComparison.Ordinal))
            return;

        _undo.Execute(new SetFontCommand(
            model,
            model.FontFamily, model.FontSize, model.FontWeight, model.Color,
            family, size, weight, color,
            After));
    }

    public void SetHidden(IReportItem item, bool hidden)
    {
        if (item is not ReportItemModel model)
            return;
        if (model.Hidden == hidden)
            return;
        _undo.Execute(new SetHiddenCommand(model, model.Hidden, hidden, After));
    }

    public void SetImage(IReportItem item, string source, string? value, string? mimeType)
    {
        if (item is not ReportItemModel model || !model.CanEditImage)
            return;

        if (string.Equals(model.ImageSource, source, StringComparison.Ordinal) &&
            string.Equals(model.ImageValue, value, StringComparison.Ordinal) &&
            string.Equals(model.ImageMimeType, mimeType, StringComparison.Ordinal))
            return;

        _undo.Execute(new SetImageCommand(
            model,
            model.ImageSource ?? "External", model.ImageValue, model.ImageMimeType,
            source, value, mimeType,
            After));
    }

    public void SetAppearance(
        IReportItem item,
        string? backgroundColor,
        string? borderColor,
        string? borderStyle,
        string? borderWidth)
    {
        if (item is not ReportItemModel model || !model.CanEditAppearance)
            return;

        if (string.Equals(model.BackgroundColor, backgroundColor, StringComparison.Ordinal) &&
            string.Equals(model.BorderColor, borderColor, StringComparison.Ordinal) &&
            string.Equals(model.BorderStyle, borderStyle, StringComparison.Ordinal) &&
            string.Equals(model.BorderWidth, borderWidth, StringComparison.Ordinal))
            return;

        _undo.Execute(new SetAppearanceCommand(
            model,
            model.BackgroundColor, model.BorderColor, model.BorderStyle, model.BorderWidth,
            backgroundColor, borderColor, borderStyle, borderWidth,
            After));
    }

    private void After()
    {
        _document()?.NotifyChanged();
        _afterChange?.Invoke();
    }

    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < 0.01;
}
