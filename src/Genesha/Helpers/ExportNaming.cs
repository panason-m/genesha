namespace Genesha.Helpers;

/// <summary>Builds a suggested file name for export Save-As dialogs, shared by notes,
/// Mermaid charts, and whiteboards.</summary>
public static class ExportNaming
{
    public static string BuildSuggestedFileName(string baseName, string extension)
    {
        var sanitized = SanitizeFileNameComponent(baseName);
        return $"{sanitized}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}";
    }

    private static string SanitizeFileNameComponent(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Export" : sanitized;
    }
}
