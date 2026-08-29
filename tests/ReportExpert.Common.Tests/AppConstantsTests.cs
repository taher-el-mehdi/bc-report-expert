using ReportExpert.Common;

namespace ReportExpert.Common.Tests;

public class AppConstantsTests
{
    [Fact]
    public void ApplicationName_IsReportExpert()
    {
        Assert.Equal("Report Expert", AppConstants.ApplicationName);
        Assert.Equal("ReportExpert", AppConstants.AppDataFolderName);
    }

    [Fact]
    public void SettingsFileNames_AreStable()
    {
        Assert.Equal("settings.json", AppConstants.SettingsFileName);
        Assert.Equal("copilot.json", AppConstants.CopilotSettingsFileName);
        Assert.Equal("recent.json", AppConstants.RecentFilesFileName);
        Assert.Equal("recent-projects.json", AppConstants.RecentProjectsFileName);
    }

    [Fact]
    public void ThemeNames_MatchPersistedValues()
    {
        Assert.Equal("System", ThemeNames.System);
        Assert.Equal("Light", ThemeNames.Light);
        Assert.Equal("Dark", ThemeNames.Dark);
    }
}
