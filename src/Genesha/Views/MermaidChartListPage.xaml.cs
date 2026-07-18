using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Genesha.Helpers;
using Genesha.Services;
using Genesha.ViewModels;

namespace Genesha.Views;

public sealed partial class MermaidChartListPage : Page
{
    private readonly MermaidChartService _mermaidChartService = App.Services.GetRequiredService<MermaidChartService>();
    private readonly WorkspaceFileWatcherService _fileWatcher = App.Services.GetRequiredService<WorkspaceFileWatcherService>();
    private readonly CurrentWorkspaceService _currentWorkspace = App.Services.GetRequiredService<CurrentWorkspaceService>();
    private string _searchText = "";

    public MermaidChartListPage()
    {
        InitializeComponent();
        Loaded += MermaidChartListPage_Loaded;
        Unloaded += MermaidChartListPage_Unloaded;
    }

    private async void MermaidChartListPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
        _fileWatcher.WorkspaceDataChanged += OnWorkspaceDataChanged;
    }

    private void MermaidChartListPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _fileWatcher.WorkspaceDataChanged -= OnWorkspaceDataChanged;
    }

    private void OnWorkspaceDataChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(async () => await ReloadAsync());

    private async Task ReloadAsync()
    {
        var charts = await _mermaidChartService.GetMermaidChartsAsync(_searchText);
        var items = charts.Select(c => new MermaidChartListItemVm(c)).ToList();
        ChartsList.ItemsSource = items;
        EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _searchText = sender.Text;
        await ReloadAsync();
    }

    private async void NewMermaidChartButton_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { PlaceholderText = "Mermaid Chart name" };
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "New Mermaid Chart",
            Content = nameBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var name = string.IsNullOrWhiteSpace(nameBox.Text) ? "Untitled Chart" : nameBox.Text.Trim();
        await _mermaidChartService.CreateMermaidChartAsync(name, "flowchart TD\n");
        await ReloadAsync();
    }

    private void ChartsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        var vm = (MermaidChartListItemVm)e.ClickedItem;
        Frame.Navigate(typeof(MermaidChartEditorPage), new MermaidChartEditorPage.NavigationArgs(vm.Id, vm.Name));
    }

    private void ChartMenuButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MermaidChartListItemVm)((FrameworkElement)sender).Tag;
        var flyout = new MenuFlyout();

        var renameItem = new MenuFlyoutItem { Text = "Rename" };
        renameItem.Click += async (_, _) => await RenameChartAsync(vm);
        flyout.Items.Add(renameItem);

        var deleteItem = new MenuFlyoutItem { Text = "Delete" };
        deleteItem.Click += async (_, _) => await DeleteChartAsync(vm);
        flyout.Items.Add(deleteItem);

        var copyReferenceItem = new MenuFlyoutItem { Text = "Copy Reference" };
        copyReferenceItem.Click += (_, _) => ReferenceText.Copy(
            ReferenceText.ForDiagram(vm.Id, vm.Name, _currentWorkspace.WorkspaceName ?? ""));
        flyout.Items.Add(copyReferenceItem);

        flyout.ShowAt((FrameworkElement)sender);
    }

    private async Task RenameChartAsync(MermaidChartListItemVm vm)
    {
        var nameBox = new TextBox { Text = vm.Name };
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Rename Mermaid Chart",
            Content = nameBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(nameBox.Text)) return;

        await _mermaidChartService.RenameMermaidChartAsync(vm.Id, nameBox.Text.Trim());
        await ReloadAsync();
    }

    private async Task DeleteChartAsync(MermaidChartListItemVm vm)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Delete Mermaid Chart",
            Content = $"Delete \"{vm.Name}\"? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await _mermaidChartService.DeleteMermaidChartAsync(vm.Id);
        await ReloadAsync();
    }
}
