using Genesha.Services;

namespace Genesha.ViewModels;

public class MermaidChartListItemVm
{
    public MermaidChartListItemVm(MermaidChartListItem item)
    {
        Id = item.Id;
        Name = item.Name;
        UpdatedAtText = item.UpdatedAt.ToString("MMM d, yyyy h:mm tt");
        SubtitleText = string.IsNullOrWhiteSpace(item.SourceWhiteboardName)
            ? UpdatedAtText
            : $"{UpdatedAtText} · From: {item.SourceWhiteboardName}";
    }

    public int Id { get; }
    public string Name { get; }
    public string UpdatedAtText { get; }
    public string SubtitleText { get; }
}
