using Genesha.Models;

namespace Genesha.Services;

/// <summary>
/// Holds which workspace folder is currently open, since (unlike Tossakan's single fixed app-data
/// root) Genesha's data location is dynamic and can change mid-session when the user switches
/// workspaces. Shared by WorkspaceDbContextFactory (to locate the SQLite file) and the
/// Note/Whiteboard services (to resolve RelativeFilePath against the workspace root).
/// </summary>
public class CurrentWorkspaceService
{
    public string? WorkspaceId { get; private set; }
    public string? WorkspaceName { get; private set; }
    public string? WorkspaceFolderPath { get; private set; }

    public event EventHandler? Changed;

    public void SetCurrent(WorkspaceRegistryEntry entry)
    {
        WorkspaceId = entry.Id;
        WorkspaceName = entry.Name;
        WorkspaceFolderPath = entry.Path;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public string RequireFolderPath() =>
        WorkspaceFolderPath ?? throw new InvalidOperationException("No workspace is currently open.");

    public string DbPath => Path.Combine(RequireFolderPath(), ".genesha", "genesha.db");
}
