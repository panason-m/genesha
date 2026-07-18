using System.Text.RegularExpressions;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace Genesha.Helpers;

/// <summary>Renders a small, deliberately-limited markdown subset (headers, bullet/numbered lists,
/// bold/italic/code) as WinUI elements, for read-only display such as the User Manual page.</summary>
public static class MarkdownRenderer
{
    private static readonly Regex HeaderPattern = new(@"^(#{1,3})\s+(.*)$");
    private static readonly Regex BulletPattern = new(@"^[-*]\s+(.*)$");
    private static readonly Regex NumberedPattern = new(@"^(\d+)\.\s+(.*)$");
    private static readonly Regex InlinePattern = new(@"(`[^`]+`|\*\*[^*]+\*\*|\*[^*]+\*|_[^_]+_)");

    public static UIElement Render(string? text)
    {
        var panel = new StackPanel { Spacing = 2 };
        if (string.IsNullOrWhiteSpace(text)) return panel;

        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (line.Length == 0)
            {
                panel.Children.Add(new Border { Height = 6 });
                continue;
            }

            var headerMatch = HeaderPattern.Match(line);
            if (headerMatch.Success)
            {
                var block = BuildInlineTextBlock(headerMatch.Groups[2].Value);
                block.FontSize = headerMatch.Groups[1].Value.Length switch { 1 => 19, 2 => 16, _ => 14 };
                block.FontWeight = FontWeights.SemiBold;
                block.Margin = new Thickness(0, 6, 0, 0);
                panel.Children.Add(block);
                continue;
            }

            var bulletMatch = BulletPattern.Match(line);
            if (bulletMatch.Success)
            {
                panel.Children.Add(BuildListRow("•", bulletMatch.Groups[1].Value));
                continue;
            }

            var numberedMatch = NumberedPattern.Match(line);
            if (numberedMatch.Success)
            {
                panel.Children.Add(BuildListRow(numberedMatch.Groups[1].Value + ".", numberedMatch.Groups[2].Value));
                continue;
            }

            panel.Children.Add(BuildInlineTextBlock(line));
        }

        return panel;
    }

    private static Grid BuildListRow(string marker, string text)
    {
        // A horizontal StackPanel measures children with unbounded width, so wrapping text inside one
        // never wraps; a two-column Grid constrains the text column to the remaining width instead.
        var row = new Grid { ColumnSpacing = 6 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var markerBlock = new TextBlock { Text = marker, FontWeight = FontWeights.Bold };
        Grid.SetColumn(markerBlock, 0);
        row.Children.Add(markerBlock);

        var textBlock = BuildInlineTextBlock(text);
        Grid.SetColumn(textBlock, 1);
        row.Children.Add(textBlock);

        return row;
    }

    private static TextBlock BuildInlineTextBlock(string text)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
        foreach (var part in InlinePattern.Split(text))
        {
            if (part.Length == 0) continue;

            if (part.Length >= 4 && part.StartsWith("**") && part.EndsWith("**"))
                block.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = part[2..^2], FontWeight = FontWeights.Bold });
            else if (part.Length >= 2 && part[0] == '`' && part[^1] == '`')
                block.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = part[1..^1], FontFamily = new FontFamily("Consolas") });
            else if (part.Length >= 2 && (part[0] == '*' || part[0] == '_') && part[^1] == part[0])
                block.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = part[1..^1], FontStyle = FontStyle.Italic });
            else
                block.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = part });
        }
        return block;
    }
}
