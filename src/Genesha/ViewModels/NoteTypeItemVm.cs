using CommunityToolkit.Mvvm.ComponentModel;
using Genesha.Models;

namespace Genesha.ViewModels;

public partial class NoteTypeItemVm : ObservableObject
{
    public NoteTypeItemVm(NoteType noteType)
    {
        Id = noteType.Id;
        name = noteType.Name;
    }

    public int Id { get; }

    [ObservableProperty]
    private string name;
}
