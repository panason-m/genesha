using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Genesha.Services;
using Genesha.ViewModels;

namespace Genesha.Views;

public sealed partial class NoteTypeSettingsPage : Page
{
    private readonly NoteTypeService _noteTypeService = App.Services.GetRequiredService<NoteTypeService>();

    public NoteTypeSettingsPage()
    {
        InitializeComponent();
        Loaded += NoteTypeSettingsPage_Loaded;
    }

    private async void NoteTypeSettingsPage_Loaded(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async Task ReloadAsync()
    {
        var types = await _noteTypeService.GetAllAsync();
        TypesList.ItemsSource = types.Select(t => new NoteTypeItemVm(t)).ToList();
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NewTypeNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        await _noteTypeService.CreateAsync(name);
        NewTypeNameBox.Text = "";
        await ReloadAsync();
    }

    private async void TypeNameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var vm = (NoteTypeItemVm)((FrameworkElement)sender).Tag;
        if (string.IsNullOrWhiteSpace(vm.Name)) return;
        await _noteTypeService.RenameAsync(vm.Id, vm.Name.Trim());
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = (NoteTypeItemVm)((FrameworkElement)sender).Tag;
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Delete Note Type",
            Content = $"Delete \"{vm.Name}\"? Notes using this type will become untyped.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await _noteTypeService.DeleteAsync(vm.Id);
        await ReloadAsync();
    }
}
