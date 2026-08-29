using System.Windows;

namespace ReportExpert.RdlcDesigner.Extensibility;

/// <summary>Drag-drop payload format for toolbox → design surface.</summary>
public static class ToolboxDragFormats
{
    public const string Kind = "ReportExpert.ToolboxItemKind";

    public static DataObject CreateData(ToolboxItemKind kind)
    {
        var data = new DataObject();
        data.SetData(Kind, kind);
        data.SetData(typeof(ToolboxItemKind), kind);
        return data;
    }

    public static bool TryGetKind(IDataObject data, out ToolboxItemKind kind)
    {
        kind = ToolboxItemKind.Pointer;
        if (data.GetDataPresent(Kind) && data.GetData(Kind) is ToolboxItemKind k1)
        {
            kind = k1;
            return kind != ToolboxItemKind.Pointer;
        }

        if (data.GetDataPresent(typeof(ToolboxItemKind)) && data.GetData(typeof(ToolboxItemKind)) is ToolboxItemKind k2)
        {
            kind = k2;
            return kind != ToolboxItemKind.Pointer;
        }

        return false;
    }
}
