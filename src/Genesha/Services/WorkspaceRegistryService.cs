using System.Text.Json;
using Genesha.Models;

namespace Genesha.Services;

/// <summary>
/// Persists the list of known workspaces as a small JSON file under %LOCALAPPDATA%\Genesha\, since
/// workspaces themselves live in user-chosen folders anywhere on disk and the app needs to remember
/// where they are across sessions (same flat-JSON rationale as Tossakan's AppSettingsService).
/// </summary>
public class WorkspaceRegistryService
{
    private WorkspaceRegistry? _cached;

    public async Task<WorkspaceRegistry> LoadAsync()
    {
        if (_cached is not null) return _cached;
        if (File.Exists(AppPaths.WorkspaceRegistryPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(AppPaths.WorkspaceRegistryPath);
                _cached = JsonSerializer.Deserialize<WorkspaceRegistry>(json) ?? new WorkspaceRegistry();
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                _cached = new WorkspaceRegistry();
            }
        }
        else
        {
            _cached = new WorkspaceRegistry();
        }
        return _cached;
    }

    public async Task<WorkspaceRegistryEntry> CreateWorkspaceAsync(string parentFolderPath, string name)
    {
        var folderPath = Path.Combine(parentFolderPath, SanitizeFolderName(name));
        Directory.CreateDirectory(folderPath);
        EnsureWorkspaceSubfolders(folderPath);

        var entry = new WorkspaceRegistryEntry
        {
            Name = name,
            Path = folderPath,
            CreatedAt = DateTime.Now,
        };

        var registry = await LoadAsync();
        registry.Workspaces.Add(entry);
        await SaveAsync(registry);
        return entry;
    }

    public async Task<WorkspaceRegistryEntry> AddExistingWorkspaceAsync(string folderPath)
    {
        var normalized = Path.GetFullPath(folderPath).TrimEnd('\\', '/');
        var registry = await LoadAsync();
        var existing = registry.Workspaces.FirstOrDefault(w =>
            string.Equals(Path.GetFullPath(w.Path).TrimEnd('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        EnsureWorkspaceSubfolders(normalized);
        var entry = new WorkspaceRegistryEntry
        {
            Name = Path.GetFileName(normalized),
            Path = normalized,
            CreatedAt = DateTime.Now,
        };
        registry.Workspaces.Add(entry);
        await SaveAsync(registry);
        return entry;
    }

    public async Task RenameWorkspaceAsync(string id, string newName)
    {
        var registry = await LoadAsync();
        var entry = registry.Workspaces.FirstOrDefault(w => w.Id == id);
        if (entry is null) return;
        entry.Name = newName;
        await SaveAsync(registry);
    }

    public async Task RemoveWorkspaceAsync(string id, bool deleteFolderFromDisk)
    {
        var registry = await LoadAsync();
        var entry = registry.Workspaces.FirstOrDefault(w => w.Id == id);
        if (entry is null) return;

        registry.Workspaces.Remove(entry);
        if (registry.LastOpenedWorkspaceId == id) registry.LastOpenedWorkspaceId = null;
        await SaveAsync(registry);

        if (deleteFolderFromDisk && Directory.Exists(entry.Path))
            Directory.Delete(entry.Path, recursive: true);
    }

    public async Task<WorkspaceRegistryEntry?> GetLastOpenedAsync()
    {
        var registry = await LoadAsync();
        return registry.Workspaces.FirstOrDefault(w => w.Id == registry.LastOpenedWorkspaceId);
    }

    public async Task SetLastOpenedAsync(string id)
    {
        var registry = await LoadAsync();
        var entry = registry.Workspaces.FirstOrDefault(w => w.Id == id);
        if (entry is null) return;
        entry.LastOpenedAt = DateTime.Now;
        registry.LastOpenedWorkspaceId = id;
        await SaveAsync(registry);
    }

    private async Task SaveAsync(WorkspaceRegistry registry)
    {
        _cached = registry;
        AppPaths.EnsureFolders();
        await File.WriteAllTextAsync(AppPaths.WorkspaceRegistryPath, JsonSerializer.Serialize(registry));
    }

    private static void EnsureWorkspaceSubfolders(string workspaceFolderPath)
    {
        Directory.CreateDirectory(Path.Combine(workspaceFolderPath, ".genesha"));
        Directory.CreateDirectory(Path.Combine(workspaceFolderPath, "Notes"));
        Directory.CreateDirectory(Path.Combine(workspaceFolderPath, "Whiteboards"));
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "Workspace" : result;
    }
}
