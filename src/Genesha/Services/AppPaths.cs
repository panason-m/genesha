namespace Genesha.Services;

public static class AppPaths
{
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Genesha");

    public static string WorkspaceRegistryPath { get; } = Path.Combine(DataFolder, "workspaces.json");

    public static void EnsureFolders()
    {
        Directory.CreateDirectory(DataFolder);
    }
}
