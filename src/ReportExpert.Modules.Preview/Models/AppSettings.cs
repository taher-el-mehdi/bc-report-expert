using ReportExpert.Common;

namespace ReportExpert.Modules.Preview.Models;

/// <summary>User preferences for preview, theme, and recent files.</summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = ThemeNames.System;
    public string DefaultExportFolder { get; set; } = string.Empty;
    public int RowsGenerated { get; set; } = AppSettingDefaults.RowsGenerated;
    public int FontSize { get; set; } = AppSettingDefaults.FontSize;
    public bool RememberRecentFiles { get; set; } = true;
    public int MaxRecentFiles { get; set; } = AppSettingDefaults.MaxRecentFiles;
    public bool AutoPreview { get; set; } = true;
    public bool AlSourcePanelVisible { get; set; } = true;
    public double AlSourcePanelWidth { get; set; } = AppSettingDefaults.AlSourcePanelWidth;

    /// <summary>Show top/left measurement rulers on the RDLC layout designer.</summary>
    public bool ShowDesignerRulers { get; set; } = true;

    /// <summary>Show Paint-style gridlines on the RDLC layout designer page.</summary>
    public bool ShowDesignerGridlines { get; set; } = true;
}

/// <summary>Default numeric preferences for <see cref="AppSettings"/>.</summary>
public static class AppSettingDefaults
{
    public const int RowsGenerated = 20;
    public const int FontSize = 14;
    public const int MaxRecentFiles = 10;
    public const double AlSourcePanelWidth = 380;
}
