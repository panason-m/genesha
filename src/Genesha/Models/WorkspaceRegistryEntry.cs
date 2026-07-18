namespace Genesha.Models;

public class WorkspaceRegistryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastOpenedAt { get; set; }
}

public class WorkspaceRegistry
{
    public List<WorkspaceRegistryEntry> Workspaces { get; set; } = new();
    public string? LastOpenedWorkspaceId { get; set; }
}
