using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Genesha.Data;
using Genesha.Helpers;
using Genesha.Services;
using Genesha.ViewModels;

namespace Genesha.Views;

public sealed partial class WorkspacePage : Page
{
    private readonly WorkspaceRegistryService _registryService =
        App.Services.GetRequiredService<WorkspaceRegistryService>();
    private readonly CurrentWorkspaceService _currentWorkspace =
        App.Services.GetRequiredService<CurrentWorkspaceService>();
    private readonly IDbContextFactory<WorkspaceDbContext> _dbContextFactory =
        App.Services.GetRequiredService<IDbContextFactory<WorkspaceDbContext>>();

    public WorkspacePage()
    {
        InitializeComponent();
        Loaded += WorkspacePage_Loaded;
    }

    private async void WorkspacePage_Loaded(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async Task ReloadAsync()
    {
        var registry = await _registryService.LoadAsync();
        var items = registry.Workspaces
            .OrderByDescending(w => w.LastOpenedAt ?? w.CreatedAt)
            .Select(w => new WorkspaceItemVm(w))
            .ToList();
        WorkspacesList.ItemsSource = items;
        EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void NewWorkspaceButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder is null) return;

        var nameBox = new TextBox { PlaceholderText = "Workspace name", Text = Path.GetFileName(folder.Path) };
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "New Workspace",
            Content = nameBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var name = string.IsNullOrWhiteSpace(nameBox.Text) ? "Workspace" : nameBox.Text.Trim();
        await _registryService.CreateWorkspaceAsync(folder.Path, name);
        await ReloadAsync();
    }

    private async void OpenExistingFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder is null) return;

        await _registryService.AddExistingWorkspaceAsync(folder.Path);
        await ReloadAsync();
    }

    private async Task<Windows.Storage.StorageFolder?> PickFolderAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        return await picker.PickSingleFolderAsync();
    }

    private async void WorkspacesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        var vm = (WorkspaceItemVm)e.ClickedItem;
        await OpenWorkspaceAsync(vm);
    }

    private async Task OpenWorkspaceAsync(WorkspaceItemVm vm)
    {
        var registry = await _registryService.LoadAsync();
        var entry = registry.Workspaces.First(w => w.Id == vm.Id);

        _currentWorkspace.SetCurrent(entry);
        await DatabaseInitializer.InitializeAsync(_dbContextFactory);
        await _registryService.SetLastOpenedAsync(vm.Id);

        var mainWindow = (MainWindow)App.MainWindow!;
        mainWindow.ShowWorkspaceNav(entry.Name);
        mainWindow.NavigationFrame.Navigate(typeof(WorkspaceDashboardPage));
    }

    private void WorkspaceMenuButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = (WorkspaceItemVm)((FrameworkElement)sender).Tag;
        var flyout = new MenuFlyout();

        var renameItem = new MenuFlyoutItem { Text = "Rename" };
        renameItem.Click += async (_, _) => await RenameWorkspaceAsync(vm);
        flyout.Items.Add(renameItem);

        var removeItem = new MenuFlyoutItem { Text = "Remove" };
        removeItem.Click += async (_, _) => await RemoveWorkspaceAsync(vm);
        flyout.Items.Add(removeItem);

        var copyReferenceItem = new MenuFlyoutItem { Text = "Copy Reference" };
        copyReferenceItem.Click += (_, _) => ReferenceText.Copy(ReferenceText.ForWorkspace(vm.Name));
        flyout.Items.Add(copyReferenceItem);

        flyout.ShowAt((FrameworkElement)sender);
    }

    private async Task RenameWorkspaceAsync(WorkspaceItemVm vm)
    {
        var nameBox = new TextBox { Text = vm.Name };
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Rename Workspace",
            Content = nameBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(nameBox.Text)) return;

        await _registryService.RenameWorkspaceAsync(vm.Id, nameBox.Text.Trim());
        await ReloadAsync();
    }

    private async Task RemoveWorkspaceAsync(WorkspaceItemVm vm)
    {
        var deleteCheckbox = new CheckBox { Content = "Also delete the folder from disk" };
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Remove Workspace",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = $"Remove \"{vm.Name}\" from the workspace list?", TextWrapping = TextWrapping.Wrap },
                    deleteCheckbox,
                },
            },
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await _registryService.RemoveWorkspaceAsync(vm.Id, deleteCheckbox.IsChecked == true);
        await ReloadAsync();
    }
}
