using Genesha.Services;

namespace Genesha.ViewModels;

public class WhiteboardListItemVm
{
    public WhiteboardListItemVm(WhiteboardListItem item)
    {
        Id = item.Id;
        Name = item.Name;
        UpdatedAtText = item.UpdatedAt.ToString("MMM d, yyyy h:mm tt");
    }

    public int Id { get; }
    public string Name { get; }
    public string UpdatedAtText { get; }
}
