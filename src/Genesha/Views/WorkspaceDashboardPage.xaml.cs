using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Genesha.Helpers;
using Genesha.Services;

namespace Genesha.Views;

public sealed partial class WorkspaceDashboardPage : Page
{
    private readonly CurrentWorkspaceService _currentWorkspace =
        App.Services.GetRequiredService<CurrentWorkspaceService>();
    private readonly NoteService _noteService = App.Services.GetRequiredService<NoteService>();
    private readonly WhiteboardService _whiteboardService = App.Services.GetRequiredService<WhiteboardService>();
    private readonly MermaidChartService _mermaidChartService = App.Services.GetRequiredService<MermaidChartService>();
    private readonly WorkspaceFileWatcherService _fileWatcher = App.Services.GetRequiredService<WorkspaceFileWatcherService>();

    public WorkspaceDashboardPage()
    {
        InitializeComponent();
        Loaded += WorkspaceDashboardPage_Loaded;
        Unloaded += WorkspaceDashboardPage_Unloaded;
    }

    private async void WorkspaceDashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadCountsAsync();
        _fileWatcher.WorkspaceDataChanged += OnWorkspaceDataChanged;
    }

    private void WorkspaceDashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _fileWatcher.WorkspaceDataChanged -= OnWorkspaceDataChanged;
    }

    private void OnWorkspaceDataChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(async () => await ReloadCountsAsync());

    private async Task ReloadCountsAsync()
    {
        WorkspaceNameText.Text = _currentWorkspace.WorkspaceName ?? "Workspace";
        NoteCountText.Text = (await _noteService.GetNoteCountAsync()).ToString();
        WhiteboardCountText.Text = (await _whiteboardService.GetWhiteboardCountAsync()).ToString();
        MermaidChartCountText.Text = (await _mermaidChartService.GetMermaidChartCountAsync()).ToString();
    }

    private void NotesTile_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(NoteListPage));

    private void ManageNoteTypes_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(NoteTypeSettingsPage));

    private void WhiteboardsTile_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(WhiteboardListPage));

    private void MermaidChartsTile_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(MermaidChartListPage));

    private void CopyWorkspaceReference_Click(object sender, RoutedEventArgs e) =>
        ReferenceText.Copy(ReferenceText.ForWorkspace(_currentWorkspace.WorkspaceName ?? ""));
}
