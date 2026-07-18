using Genesha.Services;

namespace Genesha.ViewModels;

public class NoteListItemVm
{
    public NoteListItemVm(NoteListItem item)
    {
        Id = item.Id;
        Name = item.Name;
        UpdatedAtText = item.UpdatedAt.ToString("MMM d, yyyy h:mm tt");
        NoteTypeId = item.NoteTypeId;
        TypeName = item.NoteTypeName ?? "No type";
    }

    public int Id { get; }
    public string Name { get; }
    public string UpdatedAtText { get; }
    public int? NoteTypeId { get; }
    public string TypeName { get; }
}
