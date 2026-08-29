using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Model;

namespace ReportExpert.RdlcDesigner.Services;

public sealed class SetGeometryCommand : IDesignerCommand
{
    private readonly ReportItemModel _item;
    private readonly double _oldL, _oldT, _oldW, _oldH;
    private readonly double _newL, _newT, _newW, _newH;
    private readonly Action? _after;

    public SetGeometryCommand(
        ReportItemModel item,
        double oldL, double oldT, double oldW, double oldH,
        double newL, double newT, double newW, double newH,
        Action? after = null)
    {
        _item = item;
        _oldL = oldL; _oldT = oldT; _oldW = oldW; _oldH = oldH;
        _newL = newL; _newT = newT; _newW = newW; _newH = newH;
        _after = after;
    }

    public string Name => "Move/Resize";

    public void Execute()
    {
        _item.ApplyGeometry(_newL, _newT, _newW, _newH);
        _after?.Invoke();
    }

    public void Unexecute()
    {
        _item.ApplyGeometry(_oldL, _oldT, _oldW, _oldH);
        _after?.Invoke();
    }
}

public sealed class SetNameCommand : IDesignerCommand
{
    private readonly ReportItemModel _item;
    private readonly string _oldName;
    private readonly string _newName;
    private readonly Action? _after;

    public SetNameCommand(ReportItemModel item, string oldName, string newName, Action? after = null)
    {
        _item = item;
        _oldName = oldName;
        _newName = newName;
        _after = after;
    }

    public string Name => "Set Name";

    public void Execute()
    {
        _item.ApplyName(_newName);
        _after?.Invoke();
    }

    public void Unexecute()
    {
        _item.ApplyName(_oldName);
        _after?.Invoke();
    }
}

public sealed class SetValueCommand : IDesignerCommand
{
    private readonly ReportItemModel _item;
    private readonly string? _oldValue;
    private readonly string? _newValue;
    private readonly Action? _after;

    public SetValueCommand(ReportItemModel item, string? oldValue, string? newValue, Action? after = null)
    {
        _item = item;
        _oldValue = oldValue;
        _newValue = newValue;
        _after = after;
    }

    public string Name => "Set Value";

    public void Execute()
    {
        _item.ApplyValue(_newValue);
        _after?.Invoke();
    }

    public void Unexecute()
    {
        _item.ApplyValue(_oldValue);
        _after?.Invoke();
    }
}

public sealed class SetFontCommand : IDesignerCommand
{
    private readonly ReportItemModel _item;
    private readonly string? _oldFamily, _oldSize, _oldWeight, _oldColor;
    private readonly string? _newFamily, _newSize, _newWeight, _newColor;
    private readonly Action? _after;

    public SetFontCommand(
        ReportItemModel item,
        string? oldFamily, string? oldSize, string? oldWeight, string? oldColor,
        string? newFamily, string? newSize, string? newWeight, string? newColor,
        Action? after = null)
    {
        _item = item;
        _oldFamily = oldFamily; _oldSize = oldSize; _oldWeight = oldWeight; _oldColor = oldColor;
        _newFamily = newFamily; _newSize = newSize; _newWeight = newWeight; _newColor = newColor;
        _after = after;
    }

    public string Name => "Set Font";

    public void Execute()
    {
        _item.ApplyFont(_newFamily, _newSize, _newWeight, _newColor);
        _after?.Invoke();
    }

    public void Unexecute()
    {
        _item.ApplyFont(_oldFamily, _oldSize, _oldWeight, _oldColor);
        _after?.Invoke();
    }
}

public sealed class SetHiddenCommand : IDesignerCommand
{
    private readonly ReportItemModel _item;
    private readonly bool _old;
    private readonly bool _new;
    private readonly Action? _after;

    public SetHiddenCommand(ReportItemModel item, bool oldHidden, bool newHidden, Action? after = null)
    {
        _item = item;
        _old = oldHidden;
        _new = newHidden;
        _after = after;
    }

    public string Name => "Set Hidden";

    public void Execute()
    {
        _item.ApplyHidden(_new);
        _after?.Invoke();
    }

    public void Unexecute()
    {
        _item.ApplyHidden(_old);
        _after?.Invoke();
    }
}

public sealed class SetImageCommand : IDesignerCommand
{
    private readonly ReportItemModel _item;
    private readonly string _oldSource;
    private readonly string? _oldValue;
    private readonly string? _oldMime;
    private readonly string _newSource;
    private readonly string? _newValue;
    private readonly string? _newMime;
    private readonly Action? _after;

    public SetImageCommand(
        ReportItemModel item,
        string oldSource, string? oldValue, string? oldMime,
        string newSource, string? newValue, string? newMime,
        Action? after = null)
    {
        _item = item;
        _oldSource = oldSource; _oldValue = oldValue; _oldMime = oldMime;
        _newSource = newSource; _newValue = newValue; _newMime = newMime;
        _after = after;
    }

    public string Name => "Set Image";

    public void Execute()
    {
        _item.ApplyImage(_newSource, _newValue, _newMime);
        _after?.Invoke();
    }

    public void Unexecute()
    {
        _item.ApplyImage(_oldSource, _oldValue, _oldMime);
        _after?.Invoke();
    }
}

public sealed class SetAppearanceCommand : IDesignerCommand
{
    private readonly ReportItemModel _item;
    private readonly string? _oldBg, _oldBorderColor, _oldBorderStyle, _oldBorderWidth;
    private readonly string? _newBg, _newBorderColor, _newBorderStyle, _newBorderWidth;
    private readonly Action? _after;

    public SetAppearanceCommand(
        ReportItemModel item,
        string? oldBg, string? oldBorderColor, string? oldBorderStyle, string? oldBorderWidth,
        string? newBg, string? newBorderColor, string? newBorderStyle, string? newBorderWidth,
        Action? after = null)
    {
        _item = item;
        _oldBg = oldBg; _oldBorderColor = oldBorderColor; _oldBorderStyle = oldBorderStyle; _oldBorderWidth = oldBorderWidth;
        _newBg = newBg; _newBorderColor = newBorderColor; _newBorderStyle = newBorderStyle; _newBorderWidth = newBorderWidth;
        _after = after;
    }

    public string Name => "Set Appearance";

    public void Execute()
    {
        _item.ApplyAppearance(_newBg, _newBorderColor, _newBorderStyle, _newBorderWidth);
        _after?.Invoke();
    }

    public void Unexecute()
    {
        _item.ApplyAppearance(_oldBg, _oldBorderColor, _oldBorderStyle, _oldBorderWidth);
        _after?.Invoke();
    }
}
