using Windows.ApplicationModel.DataTransfer;

namespace Genesha.Helpers;

/// <summary>Builds and copies short, self-describing reference strings for pasting into prompts.</summary>
public static class ReferenceText
{
    public static string ForWorkspace(string workspaceName)
        => $"Genesha workspace \"{workspaceName}\"";

    public static string ForNote(int id, string name, string workspaceName)
        => $"Genesha note #{id} \"{name}\" (workspace: {workspaceName})";

    public static string ForWhiteboard(int id, string name, string workspaceName)
        => $"Genesha whiteboard #{id} \"{name}\" (workspace: {workspaceName})";

    public static string ForDiagram(int id, string name, string workspaceName)
        => $"Genesha diagram #{id} \"{name}\" (workspace: {workspaceName})";

    public static void Copy(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
