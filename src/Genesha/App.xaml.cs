using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Genesha.Data;
using Genesha.Services;

namespace Genesha;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
        UnhandledException += (_, e) =>
        {
            Logger.Log($"UNHANDLED: {e.Exception}");
            e.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Log($"UNOBSERVED: {e.Exception}");
            e.SetObserved();
        };
    }

    private static IServiceProvider ConfigureServices()
    {
        AppPaths.EnsureFolders();
        var services = new ServiceCollection();
        services.AddSingleton<WorkspaceRegistryService>();
        services.AddSingleton<CurrentWorkspaceService>();
        services.AddSingleton<WorkspaceFileWatcherService>();
        services.AddSingleton<WorkspaceDbContextFactory>();
        services.AddSingleton<IDbContextFactory<WorkspaceDbContext>>(
            sp => sp.GetRequiredService<WorkspaceDbContextFactory>());
        services.AddSingleton<NoteService>();
        services.AddSingleton<NoteTypeService>();
        services.AddSingleton<WhiteboardService>();
        services.AddSingleton<MermaidChartService>();
        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
