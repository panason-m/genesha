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

public sealed partial class WhiteboardEditorPage : Page
{
    public record NavigationArgs(int WhiteboardId, string WhiteboardName);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record BridgeEnvelope(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("payload")] JsonElement Payload);

    private readonly WhiteboardService _whiteboardService = App.Services.GetRequiredService<WhiteboardService>();
    private readonly MermaidChartService _mermaidChartService = App.Services.GetRequiredService<MermaidChartService>();

    private int _whiteboardId;
    private DispatcherTimer? _saveDebounce;
    private string? _pendingSnapshotJson;

    public WhiteboardEditorPage()
    {
        InitializeComponent();
        Loaded += WhiteboardEditorPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is NavigationArgs args)
        {
            _whiteboardId = args.WhiteboardId;
            BoardNameText.Text = args.WhiteboardName;
        }
    }

    private async void WhiteboardEditorPage_Loaded(object sender, RoutedEventArgs e)
    {
        await Editor.EnsureCoreWebView2Async();

        var wwwrootPath = WebAssets.GetFolderPath("tldraw");
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
                var snapshotJson = await _whiteboardService.LoadWhiteboardSnapshotAsync(_whiteboardId);
                var snapshotValue = string.IsNullOrWhiteSpace(snapshotJson) ? "null" : snapshotJson;
                var loadPayload = "{\"type\":\"loadContent\",\"payload\":{\"snapshot\":" + snapshotValue + "}}";
                sender.PostWebMessageAsJson(loadPayload);
                break;

            case "contentChanged":
                var snapshotRaw = envelope.Payload.GetProperty("snapshot").GetRawText();
                DebounceSave(snapshotRaw);
                break;

            case "exportGroupPng":
                var pngRequestId = envelope.Payload.GetProperty("requestId").GetString()!;
                try
                {
                    var pngBytes = Convert.FromBase64String(envelope.Payload.GetProperty("pngBase64").GetString()!);
                    var fileName = await _whiteboardService.ExportGroupPngAsync(BoardNameText.Text, pngBytes);
                    PostResult(sender, "exportGroupPngResult", pngRequestId, ok: true, fileName: fileName);
                    ExportStatusText.Text = $"Exported PNG: {fileName}";
                }
                catch (Exception ex)
                {
                    PostResult(sender, "exportGroupPngResult", pngRequestId, ok: false, error: ex.Message);
                }
                break;

            case "exportFlowMermaid":
                var mermaidRequestId = envelope.Payload.GetProperty("requestId").GetString()!;
                try
                {
                    var mermaidText = envelope.Payload.GetProperty("mermaidText").GetString()!;
                    var chart = await _mermaidChartService.CreateMermaidChartAsync(
                        $"{BoardNameText.Text} Flow", mermaidText, sourceWhiteboardName: BoardNameText.Text);
                    PostResult(sender, "exportFlowMermaidResult", mermaidRequestId, ok: true, fileName: chart.Name);
                    ExportStatusText.Text = $"Exported Mermaid Chart: {chart.Name}";
                }
                catch (Exception ex)
                {
                    PostResult(sender, "exportFlowMermaidResult", mermaidRequestId, ok: false, error: ex.Message);
                }
                break;
        }
    }

    private static void PostResult(CoreWebView2 sender, string type, string requestId, bool ok, string? fileName = null, string? error = null)
    {
        var payload = JsonSerializer.Serialize(new { requestId, ok, fileName, error });
        sender.PostWebMessageAsJson($"{{\"type\":\"{type}\",\"payload\":{payload}}}");
    }

    private void DebounceSave(string snapshotJson)
    {
        _pendingSnapshotJson = snapshotJson;

        if (_saveDebounce is null)
        {
            _saveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _saveDebounce.Tick += SaveDebounce_Tick;
        }

        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private async void SaveDebounce_Tick(object? sender, object e)
    {
        _saveDebounce!.Stop();
        if (_pendingSnapshotJson is null) return;

        var snapshotJson = _pendingSnapshotJson;
        _pendingSnapshotJson = null;
        await _whiteboardService.SaveWhiteboardSnapshotAsync(_whiteboardId, snapshotJson);
        SaveStatusText.Text = $"Saved {DateTime.Now:h:mm:ss tt}";
    }
}
