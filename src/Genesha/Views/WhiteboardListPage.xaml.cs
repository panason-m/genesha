using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Genesha.Helpers;
using Genesha.Services;
using Genesha.ViewModels;

namespace Genesha.Views;

public sealed partial class WhiteboardListPage : Page
{
    private readonly WhiteboardService _whiteboardService = App.Services.GetRequiredService<WhiteboardService>();
    private readonly WorkspaceFileWatcherService _fileWatcher = App.Services.GetRequiredService<WorkspaceFileWatcherService>();
    private readonly CurrentWorkspaceService _currentWorkspace = App.Services.GetRequiredService<CurrentWorkspaceService>();
    private string _searchText = "";

    public WhiteboardListPage()
    {
        InitializeComponent();
        Loaded += WhiteboardListPage_Loaded;
        Unloaded += WhiteboardListPage_Unloaded;
    }

    private async void WhiteboardListPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
        _fileWatcher.WorkspaceDataChanged += OnWorkspaceDataChanged;
    }

    private void WhiteboardListPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _fileWatcher.WorkspaceDataChanged -= OnWorkspaceDataChanged;
    }

    private void OnWorkspaceDataChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(async () => await ReloadAsync());

    private async Task ReloadAsync()
    {
        var boards = await _whiteboardService.GetWhiteboardsAsync(_searchText);
        var items = boards.Select(b => new WhiteboardListItemVm(b)).ToList();
        BoardsList.ItemsSource = items;
        EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _searchText = sender.Text;
        await ReloadAsync();
    }

    private async void NewWhiteboardButton_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { PlaceholderText = "Whiteboard name" };
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "New Whiteboard",
            Content = nameBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var name = string.IsNullOrWhiteSpace(nameBox.Text) ? "Untitled Whiteboard" : nameBox.Text.Trim();
        await _whiteboardService.CreateWhiteboardAsync(name);
        await ReloadAsync();
    }

    private void BoardsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        var vm = (WhiteboardListItemVm)e.ClickedItem;
        Frame.Navigate(typeof(WhiteboardEditorPage), new WhiteboardEditorPage.NavigationArgs(vm.Id, vm.Name));
    }

    private void BoardMenuButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = (WhiteboardListItemVm)((FrameworkElement)sender).Tag;
        var flyout = new MenuFlyout();

        var renameItem = new MenuFlyoutItem { Text = "Rename" };
        renameItem.Click += async (_, _) => await RenameBoardAsync(vm);
        flyout.Items.Add(renameItem);

        var deleteItem = new MenuFlyoutItem { Text = "Delete" };
        deleteItem.Click += async (_, _) => await DeleteBoardAsync(vm);
        flyout.Items.Add(deleteItem);

        var copyReferenceItem = new MenuFlyoutItem { Text = "Copy Reference" };
        copyReferenceItem.Click += (_, _) => ReferenceText.Copy(
            ReferenceText.ForWhiteboard(vm.Id, vm.Name, _currentWorkspace.WorkspaceName ?? ""));
        flyout.Items.Add(copyReferenceItem);

        flyout.ShowAt((FrameworkElement)sender);
    }

    private async Task RenameBoardAsync(WhiteboardListItemVm vm)
    {
        var nameBox = new TextBox { Text = vm.Name };
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Rename Whiteboard",
            Content = nameBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(nameBox.Text)) return;

        await _whiteboardService.RenameWhiteboardAsync(vm.Id, nameBox.Text.Trim());
        await ReloadAsync();
    }

    private async Task DeleteBoardAsync(WhiteboardListItemVm vm)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Delete Whiteboard",
            Content = $"Delete \"{vm.Name}\"? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await _whiteboardService.DeleteWhiteboardAsync(vm.Id);
        await ReloadAsync();
    }
}
