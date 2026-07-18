using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Genesha.Helpers;
using Genesha.Models;
using Genesha.Services;
using Genesha.ViewModels;

namespace Genesha.Views;

public sealed partial class NoteListPage : Page
{
    private readonly NoteService _noteService = App.Services.GetRequiredService<NoteService>();
    private readonly NoteTypeService _noteTypeService = App.Services.GetRequiredService<NoteTypeService>();
    private readonly WorkspaceFileWatcherService _fileWatcher = App.Services.GetRequiredService<WorkspaceFileWatcherService>();
    private readonly CurrentWorkspaceService _currentWorkspace = App.Services.GetRequiredService<CurrentWorkspaceService>();

    private List<NoteType> _types = new();
    private string _searchText = "";
    private int? _selectedTypeId;

    public NoteListPage()
    {
        InitializeComponent();
        Loaded += NoteListPage_Loaded;
        Unloaded += NoteListPage_Unloaded;
    }

    private async void NoteListPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadTypesAsync();
        await ReloadNotesAsync();
        _fileWatcher.WorkspaceDataChanged += OnWorkspaceDataChanged;
    }

    private void NoteListPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _fileWatcher.WorkspaceDataChanged -= OnWorkspaceDataChanged;
    }

    private void OnWorkspaceDataChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await ReloadTypesAsync();
            await ReloadNotesAsync();
        });
    }

    private async Task ReloadTypesAsync()
    {
        _types = await _noteTypeService.GetAllAsync();
        var items = new List<string> { "All Types" };
        items.AddRange(_types.Select(t => t.Name));

        TypeFilterCombo.SelectionChanged -= TypeFilterCombo_SelectionChanged;
        TypeFilterCombo.ItemsSource = items;
        TypeFilterCombo.SelectedIndex = 0;
        TypeFilterCombo.SelectionChanged += TypeFilterCombo_SelectionChanged;
    }

    private async Task ReloadNotesAsync()
    {
        var notes = await _noteService.GetNotesAsync(_searchText, _selectedTypeId);
        var items = notes.Select(n => new NoteListItemVm(n)).ToList();
        NotesList.ItemsSource = items;
        EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _searchText = sender.Text;
        await ReloadNotesAsync();
    }

    private async void TypeFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = TypeFilterCombo.SelectedIndex;
        _selectedTypeId = index <= 0 ? null : _types[index - 1].Id;
        await ReloadNotesAsync();
    }

    private async void NewNoteButton_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { PlaceholderText = "Note name" };
        var typeCombo = new ComboBox
        {
            ItemsSource = new[] { "No Type" }.Concat(_types.Select(t => t.Name)).ToList(),
            SelectedIndex = 0,
        };
        var panel = new StackPanel { Spacing = 8, Children = { nameBox, typeCombo } };

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "New Note",
            Content = panel,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var name = string.IsNullOrWhiteSpace(nameBox.Text) ? "Untitled Note" : nameBox.Text.Trim();
        int? typeId = typeCombo.SelectedIndex <= 0 ? null : _types[typeCombo.SelectedIndex - 1].Id;
        await _noteService.CreateNoteAsync(name, typeId);
        await ReloadNotesAsync();
    }

    private void NotesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        var vm = (NoteListItemVm)e.ClickedItem;
        Frame.Navigate(typeof(NoteEditorPage), new NoteEditorPage.NavigationArgs(vm.Id, vm.Name));
    }

    private void NoteMenuButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = (NoteListItemVm)((FrameworkElement)sender).Tag;
        var flyout = new MenuFlyout();

        var renameItem = new MenuFlyoutItem { Text = "Rename" };
        renameItem.Click += async (_, _) => await RenameNoteAsync(vm);
        flyout.Items.Add(renameItem);

        var changeTypeItem = new MenuFlyoutItem { Text = "Change Type" };
        changeTypeItem.Click += async (_, _) => await ChangeNoteTypeAsync(vm);
        flyout.Items.Add(changeTypeItem);

        var deleteItem = new MenuFlyoutItem { Text = "Delete" };
        deleteItem.Click += async (_, _) => await DeleteNoteAsync(vm);
        flyout.Items.Add(deleteItem);

        var copyReferenceItem = new MenuFlyoutItem { Text = "Copy Reference" };
        copyReferenceItem.Click += (_, _) => ReferenceText.Copy(
            ReferenceText.ForNote(vm.Id, vm.Name, _currentWorkspace.WorkspaceName ?? ""));
        flyout.Items.Add(copyReferenceItem);

        flyout.ShowAt((FrameworkElement)sender);
    }

    private async Task RenameNoteAsync(NoteListItemVm vm)
    {
        var nameBox = new TextBox { Text = vm.Name };
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Rename Note",
            Content = nameBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(nameBox.Text)) return;

        await _noteService.RenameNoteAsync(vm.Id, nameBox.Text.Trim());
        await ReloadNotesAsync();
    }

    private async Task ChangeNoteTypeAsync(NoteListItemVm vm)
    {
        var typeCombo = new ComboBox
        {
            ItemsSource = new[] { "No Type" }.Concat(_types.Select(t => t.Name)).ToList(),
        };
        var currentIndex = vm.NoteTypeId is null ? -1 : _types.FindIndex(t => t.Id == vm.NoteTypeId);
        typeCombo.SelectedIndex = currentIndex < 0 ? 0 : currentIndex + 1;

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Change Type",
            Content = typeCombo,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        int? typeId = typeCombo.SelectedIndex <= 0 ? null : _types[typeCombo.SelectedIndex - 1].Id;
        await _noteService.SetNoteTypeAsync(vm.Id, typeId);
        await ReloadNotesAsync();
    }

    private async Task DeleteNoteAsync(NoteListItemVm vm)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Delete Note",
            Content = $"Delete \"{vm.Name}\"? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await _noteService.DeleteNoteAsync(vm.Id);
        await ReloadNotesAsync();
    }
}
