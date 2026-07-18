using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Genesha.Helpers;
using Genesha.Services;

namespace Genesha.Views;

public sealed partial class NoteEditorPage : Page
{
    public record NavigationArgs(int NoteId, string NoteName);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record BridgeEnvelope(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("payload")] JsonElement Payload);

    private readonly NoteService _noteService = App.Services.GetRequiredService<NoteService>();

    private int _noteId;
    private DispatcherTimer? _saveDebounce;
    private string? _pendingMarkdown;

    public NoteEditorPage()
    {
        InitializeComponent();
        Loaded += NoteEditorPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is NavigationArgs args)
        {
            _noteId = args.NoteId;
            NoteNameText.Text = args.NoteName;
        }
    }

    private async void NoteEditorPage_Loaded(object sender, RoutedEventArgs e)
    {
        await Editor.EnsureCoreWebView2Async();

        var wwwrootPath = WebAssets.GetFolderPath("blocknote");
        Editor.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "genesha.local", wwwrootPath, CoreWebView2HostResourceAccessKind.Allow);

        Editor.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
        Editor.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

        Editor.CoreWebView2.Navigate("https://genesha.local/index.html");
    }

    private void CoreWebView2_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (!args.Uri.StartsWith("https://genesha.local/", StringComparison.Ordinal))
            args.Cancel = true;
    }

    private async void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        var envelope = JsonSerializer.Deserialize<BridgeEnvelope>(args.WebMessageAsJson, JsonOptions);
        if (envelope is null) return;

        switch (envelope.Type)
        {
            case "ready":
                var markdown = await _noteService.LoadNoteContentAsync(_noteId);
                var loadPayload = JsonSerializer.Serialize(new { type = "loadContent", payload = new { markdown } });
                sender.PostWebMessageAsJson(loadPayload);
                break;

            case "contentChanged":
                var changedMarkdown = envelope.Payload.GetProperty("markdown").GetString() ?? "";
                DebounceSave(changedMarkdown);
                break;
        }
    }

    private void DebounceSave(string markdown)
    {
        _pendingMarkdown = markdown;

        if (_saveDebounce is null)
        {
            _saveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _saveDebounce.Tick += SaveDebounce_Tick;
        }

        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private async void SaveDebounce_Tick(object? sender, object e)
    {
        _saveDebounce!.Stop();
        if (_pendingMarkdown is null) return;

        var markdown = _pendingMarkdown;
        _pendingMarkdown = null;
        await _noteService.SaveNoteContentAsync(_noteId, markdown);
        SaveStatusText.Text = $"Saved {DateTime.Now:h:mm:ss tt}";
    }
}
