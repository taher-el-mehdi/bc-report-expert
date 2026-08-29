using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ReportExpert.Modules.Localization.Models;
using ReportExpert.Modules.Localization.Services;

namespace ReportExpert.Modules.Localization.ViewModels;

/// <summary>View model for editing report labels and exporting translations.</summary>
public partial class LocalizationViewModel : ObservableObject
{
    private readonly LocalizationService _service = new();
    private string _reportXml = string.Empty;

    public ObservableCollection<ReportLabelEntry> Labels { get; } = [];
    public ObservableCollection<string> Languages { get; } = [];

    [ObservableProperty]
    private string _reportPath = string.Empty;

    [ObservableProperty]
    private string _selectedLanguage = "default";

    [ObservableProperty]
    private string _statusMessage = "Open a report to edit rd:ReportLabels.";

    [ObservableProperty]
    private ReportLabelEntry? _selectedLabel;

    public void SetReportContext(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        ReportPath = path;
        LoadReport();
    }

    [RelayCommand]
    private void BrowseReport()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Report definitions (*.rdlc;*.rdl)|*.rdlc;*.rdl|RDLC (*.rdlc)|*.rdlc|RDL (*.rdl)|*.rdl|All files (*.*)|*.*",
            Title = "Select report definition"
        };

        if (dialog.ShowDialog() != true)
            return;

        ReportPath = dialog.FileName;
        LoadReport();
    }

    [RelayCommand]
    private void AddLabel()
    {
        Labels.Add(new ReportLabelEntry
        {
            LabelName = $"Label{Labels.Count + 1}",
            Value = string.Empty,
            Language = SelectedLanguage
        });
        StatusMessage = "Added new label row. Save to write rd:ReportLabels.";
    }

    [RelayCommand]
    private void RemoveLabel()
    {
        if (SelectedLabel is null)
            return;

        Labels.Remove(SelectedLabel);
        StatusMessage = "Removed label row. Save to persist changes.";
    }

    [RelayCommand]
    private void SaveLabels()
    {
        if (string.IsNullOrWhiteSpace(_reportXml) || string.IsNullOrWhiteSpace(ReportPath))
        {
            StatusMessage = "No report loaded.";
            return;
        }

        try
        {
            string updated = _service.ApplyLabels(_reportXml, Labels, SelectedLanguage);
            File.WriteAllText(ReportPath, updated);
            _reportXml = updated;
            StatusMessage = $"Saved {Labels.Count} label(s) for language '{SelectedLanguage}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExportTranslation()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV translation file (*.csv)|*.csv",
            FileName = $"{Path.GetFileNameWithoutExtension(ReportPath)}_labels.csv",
            Title = "Export labels for translation"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            string csv = _service.ExportForTranslation(Labels);
            File.WriteAllText(dialog.FileName, csv);
            StatusMessage = $"Exported {Labels.Count} label(s) to {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ImportTranslation()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV translation file (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Import translated labels"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            string csv = File.ReadAllText(dialog.FileName);
            Labels.Clear();
            foreach (var entry in _service.ImportFromTranslationCsv(csv))
                Labels.Add(entry);

            if (Labels.Count > 0)
                SelectedLanguage = Labels[0].Language;

            StatusMessage = $"Imported {Labels.Count} label(s). Review and save to apply.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    partial void OnSelectedLanguageChanged(string value) => FilterLabelsByLanguage();

    private void LoadReport()
    {
        Labels.Clear();
        Languages.Clear();

        try
        {
            _reportXml = File.ReadAllText(ReportPath);
            foreach (string lang in _service.GetLanguages(_reportXml))
                Languages.Add(lang);

            if (Languages.Count == 0)
                Languages.Add("default");

            SelectedLanguage = Languages[0];
            foreach (var label in _service.LoadLabels(_reportXml, SelectedLanguage))
                Labels.Add(label);

            StatusMessage = Labels.Count > 0
                ? $"Loaded {Labels.Count} label(s) from {Path.GetFileName(ReportPath)}."
                : $"No rd:ReportLabels found in {Path.GetFileName(ReportPath)}. Add labels below.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
    }

    private void FilterLabelsByLanguage()
    {
        if (string.IsNullOrWhiteSpace(_reportXml))
            return;

        Labels.Clear();
        foreach (var label in _service.LoadLabels(_reportXml, SelectedLanguage))
            Labels.Add(label);
    }
}
