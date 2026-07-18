using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Genesha.Views;

namespace Genesha;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Genesha";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1360, 840));

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);

        NavView.SelectedItem = WorkspacesItem;
        RootFrame.Navigate(typeof(WorkspacePage));
    }

    public Frame NavigationFrame => RootFrame;

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (RootFrame.CanGoBack) RootFrame.GoBack();
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item) return;
        switch (item.Tag as string)
        {
            case "workspaces":
                HideWorkspaceNav();
                RootFrame.Navigate(typeof(WorkspacePage));
                break;
            case "dashboard": RootFrame.Navigate(typeof(WorkspaceDashboardPage)); break;
            case "notes": RootFrame.Navigate(typeof(NoteListPage)); break;
            case "noteTypes": RootFrame.Navigate(typeof(NoteTypeSettingsPage)); break;
            case "whiteboards": RootFrame.Navigate(typeof(WhiteboardListPage)); break;
            case "mermaidCharts": RootFrame.Navigate(typeof(MermaidChartListPage)); break;
            case "manual": RootFrame.Navigate(typeof(UserManualPage)); break;
        }
    }

    private void RootFrame_Navigated(object sender, NavigationEventArgs e)
    {
        BackButton.IsEnabled = RootFrame.CanGoBack;
    }

    public void ShowWorkspaceNav(string workspaceName)
    {
        CurrentWorkspaceText.Text = workspaceName;
        DashboardItem.Visibility = Visibility.Visible;
        NotesItem.Visibility = Visibility.Visible;
        NoteTypesItem.Visibility = Visibility.Visible;
        WhiteboardsItem.Visibility = Visibility.Visible;
        MermaidChartsItem.Visibility = Visibility.Visible;
    }

    public void HideWorkspaceNav()
    {
        CurrentWorkspaceText.Text = "";
        DashboardItem.Visibility = Visibility.Collapsed;
        NotesItem.Visibility = Visibility.Collapsed;
        NoteTypesItem.Visibility = Visibility.Collapsed;
        WhiteboardsItem.Visibility = Visibility.Collapsed;
        MermaidChartsItem.Visibility = Visibility.Collapsed;
    }
}
