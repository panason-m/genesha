using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Genesha.Helpers;

public static class FilePickers
{
    /// <summary>Shows the native Save As dialog. <paramref name="settingsIdentifier"/> lets Windows
    /// remember the last-used folder separately per export flow (Markdown vs PDF vs PNG, etc.).</summary>
    public static async Task<StorageFile?> PickSaveFileAsync(
        string suggestedFileName, string extension, string fileTypeDescription, string settingsIdentifier)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SettingsIdentifier = settingsIdentifier,
        };
        picker.FileTypeChoices.Add(fileTypeDescription, new List<string> { extension });

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        return await picker.PickSaveFileAsync();
    }
}
