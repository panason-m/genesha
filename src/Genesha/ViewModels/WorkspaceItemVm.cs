using Genesha.Models;

namespace Genesha.ViewModels;

public class WorkspaceItemVm
{
    public WorkspaceItemVm(WorkspaceRegistryEntry entry)
    {
        Id = entry.Id;
        Name = entry.Name;
        Path = entry.Path;
        LastOpenedText = entry.LastOpenedAt is { } d ? $"Last opened {d:MMM d, yyyy h:mm tt}" : "Never opened";
    }

    public string Id { get; }
    public string Name { get; }
    public string Path { get; }
    public string LastOpenedText { get; }
}
