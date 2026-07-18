namespace Genesha.Helpers;

public static class WebAssets
{
    public static string GetFolderPath(string bundleName) =>
        Path.Combine(AppContext.BaseDirectory, "wwwroot", bundleName);
}
