using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel.DataTransfer;
using Genesha.Helpers;
using Genesha.Services;

namespace Genesha.Views;

public sealed partial class MermaidChartEditorPage : Page
{
    public record NavigationArgs(int ChartId, string ChartName);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record BridgeEnvelope(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("payload")] JsonElement Payload);

    private readonly MermaidChartService _mermaidChartService = App.Services.GetRequiredService<MermaidChartService>();

    private int _chartId;
    private string _currentText = "";
    private string? _currentLayoutJson;
    private DispatcherTimer? _saveDebounce;
    private string? _pendingText;

    public MermaidChartEditorPage()
    {
        InitializeComponent();
        Loaded += MermaidChartEditorPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is NavigationArgs args)
        {
            _chartId = args.ChartId;
            ChartNameText.Text = args.ChartName;
        }
    }

    private async void MermaidChartEditorPage_Loaded(object sender, RoutedEventArgs e)
    {
        await Editor.EnsureCoreWebView2Async();

        var wwwrootPath = WebAssets.GetFolderPath("mermaid");
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
                _currentText = await _mermaidChartService.LoadMermaidChartTextAsync(_chartId);
                var loadPayload = JsonSerializer.Serialize(new { type = "loadContent", payload = new { text = _currentText } });
                sender.PostWebMessageAsJson(loadPayload);
                break;

            case "contentChanged":
                _currentText = envelope.Payload.GetProperty("text").GetString() ?? "";
                DebounceSave(_currentText);
                break;

            case "layout":
                _currentLayoutJson = envelope.Payload.GetProperty("nodes").GetRawText();
                break;
        }
    }

    private void DebounceSave(string text)
    {
        _pendingText = text;

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
        if (_pendingText is null) return;

        var text = _pendingText;
        _pendingText = null;
        await _mermaidChartService.SaveMermaidChartTextAsync(_chartId, text);
        SaveStatusText.Text = $"Saved {DateTime.Now:h:mm:ss tt}";
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(_currentText);
        Clipboard.SetContent(package);
    }

    /// <summary>Copies the last-reported rendered layout (node/subgraph ids, labels, bounding boxes) as
    /// JSON, so the exact rendered positions can be inspected without a screenshot.</summary>
    private void CopyLayoutButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentLayoutJson is null) return;
        var package = new DataPackage();
        package.SetText(_currentLayoutJson);
        Clipboard.SetContent(package);
    }
}
