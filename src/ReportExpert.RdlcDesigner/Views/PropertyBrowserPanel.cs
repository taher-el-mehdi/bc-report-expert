using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ReportExpert.RdlcDesigner.Properties;

namespace ReportExpert.RdlcDesigner.Views;

/// <summary>
/// Selection-aware properties browser built from <see cref="IPropertyProvider"/> categories.
/// </summary>
public sealed class PropertyBrowserPanel : UserControl
{
    private readonly StackPanel _root = new();
    private readonly TextBlock _caption = new()
    {
        FontWeight = FontWeights.SemiBold,
        FontSize = 13,
        Margin = new Thickness(0, 0, 0, 10)
    };
    private readonly TextBlock _hint = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
        FontSize = 11,
        Margin = new Thickness(0, 12, 0, 0)
    };

    private PropertyBrowserService? _browser;
    private Action? _openExpression;
    private bool _suppress;
    private bool _isDark;

    public PropertyBrowserPanel()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(14),
            Content = _root
        };
        Content = scroll;
        _root.Children.Add(_caption);
    }

    public void ApplyTheme(bool isDark)
    {
        _isDark = isDark;
        _caption.Foreground = isDark
            ? new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0))
            : new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
        _hint.Foreground = isDark
            ? new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0))
            : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        Refresh();
    }

    public void Attach(PropertyBrowserService browser, Action? openExpression = null)
    {
        if (_browser is not null)
            _browser.Changed -= OnBrowserChanged;

        _browser = browser;
        _openExpression = openExpression;
        _browser.Changed += OnBrowserChanged;
        Refresh();
    }

    private void OnBrowserChanged(object? sender, EventArgs e) => Refresh();

    public void Refresh()
    {
        if (_browser is null)
            return;

        _suppress = true;
        try
        {
            while (_root.Children.Count > 1)
                _root.Children.RemoveAt(1);

            _caption.Text = _browser.GetSelectionCaption();
            IReadOnlyList<PropertyCategory> categories = _browser.GetCategories();
            if (categories.Count == 0)
            {
                _hint.Text = "Load a report to edit properties.";
                _root.Children.Add(_hint);
                return;
            }

            foreach (PropertyCategory category in categories)
            {
                _root.Children.Add(new TextBlock
                {
                    Text = category.Name,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Foreground = _isDark
                        ? new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0))
                        : new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
                    Margin = new Thickness(0, 8, 0, 8)
                });

                foreach (PropertyDefinition prop in category.Properties)
                    _root.Children.Add(BuildEditor(prop));
            }

            _hint.Text = _browser.CreateContext().IsEmpty
                ? "Select a report item to edit its properties."
                : "Changes apply when the field loses focus.";
            _root.Children.Add(_hint);
        }
        finally
        {
            _suppress = false;
        }
    }

    private UIElement BuildEditor(PropertyDefinition prop)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(new TextBlock
        {
            Text = prop.DisplayName,
            Foreground = _isDark
                ? new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0))
                : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 3)
        });

        FrameworkElement editor = prop.Editor switch
        {
            PropertyEditorKind.Boolean => BuildCheckBox(prop),
            PropertyEditorKind.FontFamily => BuildFontFamily(prop),
            PropertyEditorKind.FontWeight => BuildFontWeight(prop),
            PropertyEditorKind.FontSize => BuildFontSize(prop),
            PropertyEditorKind.Color => BuildColor(prop),
            PropertyEditorKind.BorderStyle => BuildBorderStyle(prop),
            PropertyEditorKind.ImageSource => BuildImageSource(prop),
            PropertyEditorKind.ImageFilePath => BuildImageFilePath(prop),
            PropertyEditorKind.Expression => BuildExpression(prop),
            PropertyEditorKind.MultilineText => BuildText(prop, multiline: true),
            PropertyEditorKind.ReadOnlyLabel => BuildLabel(prop),
            PropertyEditorKind.Number => BuildText(prop, multiline: false),
            _ => BuildText(prop, multiline: false)
        };
        panel.Children.Add(editor);
        return panel;
    }

    private FrameworkElement BuildFontFamily(PropertyDefinition prop)
    {
        var combo = new ComboBox
        {
            IsEnabled = !prop.IsReadOnly,
            IsEditable = true,
            StaysOpenOnEdit = true,
            MaxDropDownHeight = 280
        };

        string current = prop.Value?.ToString() ?? string.Empty;
        foreach (string family in ReportFontCatalog.Families)
        {
            string display = ReportFontCatalog.DisplayName(family);
            var item = new ComboBoxItem
            {
                Content = display,
                Tag = family,
                ToolTip = display
            };
            if (!string.IsNullOrEmpty(family))
            {
                try { item.FontFamily = new FontFamily(family); }
                catch { /* ignore missing fonts */ }
            }

            combo.Items.Add(item);
            if (string.Equals(family, current, StringComparison.OrdinalIgnoreCase))
                combo.SelectedItem = item;
        }

        if (combo.SelectedItem is null)
        {
            if (!string.IsNullOrWhiteSpace(current))
            {
                var custom = new ComboBoxItem { Content = current, Tag = current };
                try { custom.FontFamily = new FontFamily(current); }
                catch (Exception)
                {
                    // Unknown or invalid family name from the RDLC — keep the combo item without previewing the font.
                }
                combo.Items.Insert(1, custom);
                combo.SelectedItem = custom;
            }
            else
            {
                combo.SelectedIndex = 0;
            }
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (_suppress)
                return;
            Apply(prop, GetComboValue(combo));
        };
        combo.LostFocus += (_, _) =>
        {
            if (_suppress)
                return;
            string? value = GetComboValue(combo);
            if (string.Equals(value, "Default", StringComparison.OrdinalIgnoreCase))
                value = string.Empty;
            Apply(prop, value);
        };
        return combo;
    }

    private FrameworkElement BuildColor(PropertyDefinition prop)
    {
        var combo = new ComboBox
        {
            IsEnabled = !prop.IsReadOnly,
            IsEditable = true,
            StaysOpenOnEdit = true,
            MaxDropDownHeight = 280
        };

        string current = prop.Value?.ToString() ?? string.Empty;
        if (IsInvalidStoredColor(current))
            current = string.Empty;

        foreach (ReportColorCatalog.Entry entry in ReportColorCatalog.WithDefaults)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(CreateColorSwatch(entry.Value));
            row.Children.Add(new TextBlock
            {
                Text = entry.DisplayName,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            });

            var item = new ComboBoxItem
            {
                Content = row,
                Tag = entry.Value,
                ToolTip = string.IsNullOrEmpty(entry.Value) ? "Default" : entry.Value
            };
            combo.Items.Add(item);
            if (string.Equals(entry.Value, current, StringComparison.OrdinalIgnoreCase))
                combo.SelectedItem = item;
        }

        if (combo.SelectedItem is null && !string.IsNullOrWhiteSpace(current))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(CreateColorSwatch(current));
            row.Children.Add(new TextBlock
            {
                Text = current,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            });
            var custom = new ComboBoxItem { Content = row, Tag = current, ToolTip = current };
            combo.Items.Insert(1, custom);
            combo.SelectedItem = custom;
        }
        else if (combo.SelectedItem is null)
        {
            combo.SelectedIndex = 0;
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (_suppress)
                return;
            Apply(prop, NormalizeColorValue(GetComboValue(combo)));
        };
        combo.LostFocus += (_, _) =>
        {
            if (_suppress)
                return;
            Apply(prop, NormalizeColorValue(GetComboValue(combo)));
        };
        return combo;
    }

    /// <summary>
    /// Resolves the RDL value from a ComboBox. Prefer Tag on ComboBoxItem —
    /// editable combos with complex Content expose Text as the type name.
    /// </summary>
    private static string? GetComboValue(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem { Tag: string tag })
            return tag;

        if (combo.SelectedItem is string selectedText)
            return selectedText;

        string? text = combo.Text?.Trim();
        if (string.IsNullOrEmpty(text) || IsInvalidStoredColor(text))
            return null;

        return text;
    }

    private static bool IsInvalidStoredColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Contains("System.Windows.", StringComparison.Ordinal) ||
         value.Contains("ComboBoxItem", StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeColorValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || IsInvalidStoredColor(value))
            return null;
        value = value.Trim();
        if (value.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Automatic", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("No Color", StringComparison.OrdinalIgnoreCase))
            return null;

        // Map display name ("Light Gray") back to RDL value ("LightGray") when possible.
        foreach (ReportColorCatalog.Entry entry in ReportColorCatalog.Colors)
        {
            if (entry.DisplayName.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                entry.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }

        return value;
    }

    private static Border CreateColorSwatch(string? colorName)
    {
        Brush fill = Brushes.Transparent;
        if (!string.IsNullOrWhiteSpace(colorName) && TryParseBrush(colorName, out Brush? brush) && brush is not null)
            fill = brush;

        return new Border
        {
            Width = 14,
            Height = 14,
            Background = fill,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };
    }

    private static bool TryParseBrush(string colorName, out Brush? brush)
    {
        brush = null;
        try
        {
            object? converted = ColorConverter.ConvertFromString(colorName);
            if (converted is Color c)
            {
                brush = new SolidColorBrush(c);
                brush.Freeze();
                return true;
            }
        }
        catch
        {
            // Also try British Grey spelling used in some RDL samples.
            if (colorName.Contains("Grey", StringComparison.OrdinalIgnoreCase))
                return TryParseBrush(colorName.Replace("Grey", "Gray", StringComparison.OrdinalIgnoreCase), out brush);
        }

        return false;
    }

    private FrameworkElement BuildFontSize(PropertyDefinition prop)
    {
        var combo = new ComboBox
        {
            IsEnabled = !prop.IsReadOnly,
            IsEditable = true,
            StaysOpenOnEdit = true
        };
        string[] sizes = ["", "6pt", "7pt", "8pt", "9pt", "10pt", "11pt", "12pt", "14pt", "16pt", "18pt", "20pt", "22pt", "24pt", "26pt", "28pt", "36pt", "48pt", "72pt"];
        string current = prop.Value?.ToString() ?? string.Empty;
        foreach (string size in sizes)
        {
            string display = string.IsNullOrEmpty(size) ? "Default" : size;
            combo.Items.Add(new ComboBoxItem { Content = display, Tag = size });
            if (string.Equals(size, current, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(display, current, StringComparison.OrdinalIgnoreCase))
                combo.SelectedIndex = combo.Items.Count - 1;
        }

        if (combo.SelectedIndex < 0)
        {
            if (!string.IsNullOrWhiteSpace(current))
            {
                combo.Items.Insert(1, new ComboBoxItem { Content = current, Tag = current });
                combo.SelectedIndex = 1;
            }
            else
            {
                combo.SelectedIndex = 0;
            }
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (_suppress)
                return;
            string? value = GetComboValue(combo);
            if (string.Equals(value, "Default", StringComparison.OrdinalIgnoreCase))
                value = string.Empty;
            Apply(prop, value);
        };
        combo.LostFocus += (_, _) =>
        {
            if (_suppress)
                return;
            string? value = GetComboValue(combo);
            if (string.Equals(value, "Default", StringComparison.OrdinalIgnoreCase))
                value = string.Empty;
            Apply(prop, value);
        };
        return combo;
    }

    private FrameworkElement BuildLabel(PropertyDefinition prop) =>
        new TextBlock
        {
            Text = prop.Value?.ToString() ?? string.Empty,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };

    private FrameworkElement BuildCheckBox(PropertyDefinition prop)
    {
        var box = new CheckBox
        {
            IsChecked = prop.Value is true,
            IsEnabled = !prop.IsReadOnly
        };
        box.Checked += (_, _) => Apply(prop, true);
        box.Unchecked += (_, _) => Apply(prop, false);
        return box;
    }

    private FrameworkElement BuildFontWeight(PropertyDefinition prop)
    {
        var combo = new ComboBox { IsEnabled = !prop.IsReadOnly };
        combo.Items.Add("Default");
        combo.Items.Add("Normal");
        combo.Items.Add("Bold");
        string current = prop.Value?.ToString() ?? "Default";
        combo.SelectedItem = current is "Bold" or "SemiBold" ? "Bold" : current is "Normal" ? "Normal" : "Default";
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string s)
                Apply(prop, s);
        };
        return combo;
    }

    private FrameworkElement BuildBorderStyle(PropertyDefinition prop)
    {
        var combo = new ComboBox { IsEnabled = !prop.IsReadOnly };
        string[] styles = ["None", "Solid", "Dashed", "Dotted", "Double"];
        foreach (string style in styles)
            combo.Items.Add(style);

        string current = prop.Value?.ToString() ?? "Solid";
        combo.SelectedItem = styles.Contains(current, StringComparer.OrdinalIgnoreCase)
            ? styles.First(s => s.Equals(current, StringComparison.OrdinalIgnoreCase))
            : "Solid";
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string s)
                Apply(prop, s);
        };
        return combo;
    }

    private FrameworkElement BuildImageSource(PropertyDefinition prop)
    {
        var combo = new ComboBox { IsEnabled = !prop.IsReadOnly };
        combo.Items.Add("External");
        combo.Items.Add("Embedded");
        combo.Items.Add("Database");
        combo.SelectedItem = prop.Value?.ToString() ?? "External";
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string s)
                Apply(prop, s);
        };
        return combo;
    }

    private FrameworkElement BuildImageFilePath(PropertyDefinition prop)
    {
        var dock = new DockPanel();
        var browse = new Button
        {
            Content = "…",
            Width = 32,
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "Browse for image",
            IsEnabled = !prop.IsReadOnly
        };
        DockPanel.SetDock(browse, Dock.Right);

        var box = new TextBox
        {
            Text = DisplayImagePath(prop.Value?.ToString()),
            IsReadOnly = prop.IsReadOnly,
            ToolTip = prop.Value?.ToString()
        };
        box.LostFocus += (_, _) => Apply(prop, box.Text);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Apply(prop, box.Text);
                e.Handled = true;
            }
        };

        browse.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select image",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|All files|*.*",
                CheckFileExists = true
            };

            string? current = TryResolveLocalPath(prop.Value?.ToString());
            if (!string.IsNullOrWhiteSpace(current))
            {
                try
                {
                    dlg.InitialDirectory = System.IO.Path.GetDirectoryName(current);
                    dlg.FileName = System.IO.Path.GetFileName(current);
                }
                catch { /* ignore */ }
            }

            if (dlg.ShowDialog() == true)
            {
                box.Text = dlg.FileName;
                box.ToolTip = dlg.FileName;
                Apply(prop, dlg.FileName);
            }
        };

        dock.Children.Add(browse);
        dock.Children.Add(box);
        return dock;
    }

    private static string DisplayImagePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        string? local = TryResolveLocalPath(value);
        return local ?? value;
    }

    private static string? TryResolveLocalPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        value = value.Trim().Trim('"');
        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(value);
                if (uri.IsFile)
                    return uri.LocalPath;
            }
            catch
            {
                return null;
            }
        }

        return System.IO.Path.IsPathRooted(value) ? value : null;
    }

    private FrameworkElement BuildExpression(PropertyDefinition prop)
    {
        var dock = new DockPanel();
        var fx = new Button
        {
            Content = "fx",
            Width = 32,
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "Expression editor"
        };
        DockPanel.SetDock(fx, Dock.Right);
        fx.Click += (_, _) => _openExpression?.Invoke();

        var box = new TextBox
        {
            Text = prop.Value?.ToString() ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64,
            FontFamily = new FontFamily("Consolas"),
            IsReadOnly = prop.IsReadOnly
        };
        box.LostFocus += (_, _) => Apply(prop, box.Text);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Apply(prop, box.Text);
                e.Handled = true;
            }
        };

        dock.Children.Add(fx);
        dock.Children.Add(box);
        return dock;
    }

    private FrameworkElement BuildText(PropertyDefinition prop, bool multiline)
    {
        var box = new TextBox
        {
            Text = FormatValue(prop),
            IsReadOnly = prop.IsReadOnly,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap
        };
        // MinHeight must not be NaN — WPF throws ArgumentException.
        if (multiline)
        {
            box.MinHeight = 64;
            box.FontFamily = new FontFamily("Consolas");
        }

        box.LostFocus += (_, _) => CommitText(prop, box.Text);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && (!multiline || Keyboard.Modifiers == ModifierKeys.Control))
            {
                CommitText(prop, box.Text);
                e.Handled = true;
            }
        };
        return box;
    }

    private static string FormatValue(PropertyDefinition prop)
    {
        if (prop.Value is double d)
            return d.ToString("0.##", CultureInfo.InvariantCulture);
        return prop.Value?.ToString() ?? string.Empty;
    }

    private void CommitText(PropertyDefinition prop, string text)
    {
        if (prop.Editor == PropertyEditorKind.Number)
        {
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double n))
                Apply(prop, n);
            return;
        }

        Apply(prop, text);
    }

    private void Apply(PropertyDefinition prop, object? value)
    {
        if (_suppress || prop.IsReadOnly)
            return;

        // Applying can refresh the browser; suppress re-entrant UI rebuilds from LostFocus.
        _suppress = true;
        try
        {
            prop.Apply(value);
        }
        finally
        {
            _suppress = false;
        }
    }
}
