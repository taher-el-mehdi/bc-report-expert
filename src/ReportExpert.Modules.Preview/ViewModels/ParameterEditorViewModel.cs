using CommunityToolkit.Mvvm.ComponentModel;
using ReportExpert.Modules.Preview.Models;

namespace ReportExpert.Modules.Preview.ViewModels;

/// <summary>
/// Binds a single RDLC report parameter to a UI editor control
/// (text, date, boolean, or combo) and produces the runtime value for preview.
/// </summary>
public partial class ParameterEditorViewModel : ObservableObject
{
    public RdlcParameterInfo Info { get; }

    [ObservableProperty]
    private string _textValue = string.Empty;

    [ObservableProperty]
    private DateTime? _dateValue = DateTime.Today;

    [ObservableProperty]
    private bool _boolValue;

    [ObservableProperty]
    private string? _selectedComboValue;

    public bool IsText => Info.DataType is "String" or "Integer" or "Float" && !IsCombo && !IsBoolean && !IsDate;
    public bool IsDate => Info.DataType is "DateTime";
    public bool IsBoolean => Info.DataType is "Boolean";
    public bool IsCombo => Info.ValidValues.Count > 0;

    public ParameterEditorViewModel(RdlcParameterInfo info)
    {
        Info = info;
        TextValue = info.DefaultValue ?? string.Empty;
        if (info.ValidValues.Count > 0)
            SelectedComboValue = info.ValidValues[0];
        if (IsBoolean && bool.TryParse(info.DefaultValue, out bool b))
            BoolValue = b;
    }

    /// <summary>Returns the parameter value in the format expected by the report engine.</summary>
    public object? GetValue()
    {
        if (IsBoolean) return BoolValue;
        if (IsDate) return DateValue ?? DateTime.Today;
        if (IsCombo)
        {
            if (!string.IsNullOrWhiteSpace(SelectedComboValue))
                return SelectedComboValue;
            return Info.ValidValues.FirstOrDefault() ?? DefaultForDataType();
        }

        if (!string.IsNullOrWhiteSpace(TextValue))
            return TextValue;

        return DefaultForDataType();
    }

    private object DefaultForDataType() => Info.DataType switch
    {
        "Boolean" => false,
        "Integer" => 0,
        "Float" => 0d,
        "DateTime" => DateTime.Today,
        _ => string.Empty
    };
}
