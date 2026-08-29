using ReportExpert.Domain.Models;

namespace ReportExpert.Core.Workspace;

public interface IWorkspaceService
{
    WorkspaceSnapshot? Current { get; }

    IReadOnlyList<RecentProjectEntry> RecentProjects { get; }

    event Action<WorkspaceSnapshot?>? WorkspaceChanged;

    WorkspaceSnapshot? OpenFolder(string folderPath);

    void Close();

    void Refresh();
}
