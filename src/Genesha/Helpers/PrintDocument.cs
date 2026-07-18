using System.Net;

namespace Genesha.Helpers;

/// <summary>Wraps a note's exported HTML in a clean, light, document-style page (independent of the
/// app's dark editor theme) for PDF export via WebView2's PrintToPdfAsync.</summary>
public static class PrintDocument
{
    // Plain (non-interpolated, non-composite-format) raw string, with Replace()-substituted
    // placeholders — so CSS braces are written as-is instead of needing to be doubled for C#
    // string interpolation or string.Format's own brace syntax.
    private const string Template = """
        <!doctype html>
        <html>
        <head>
        <meta charset="utf-8">
        <title>__TITLE__</title>
        <style>
          * { box-sizing: border-box; }
          body {
            margin: 0; padding: 0 4px;
            background: #ffffff; color: #1a1a1a;
            font-family: "Segoe UI", -apple-system, Arial, sans-serif;
            font-size: 12pt; line-height: 1.6;
          }
          h1, h2, h3, h4 { font-weight: 600; line-height: 1.3; margin: 1.4em 0 0.5em; }
          h1 { font-size: 22pt; }
          h2 { font-size: 18pt; }
          h3 { font-size: 14pt; }
          h4 { font-size: 12pt; }
          p { margin: 0 0 0.8em; }
          ul, ol { margin: 0 0 0.8em; padding-left: 1.4em; }
          li { margin: 0.2em 0; }
          li > p { margin: 0; }
          a { color: #2563eb; }
          strong { font-weight: 600; }
          code {
            font-family: Consolas, "Cascadia Mono", monospace; font-size: 0.9em;
            background: #f2f2f2; padding: 0.15em 0.35em; border-radius: 3px;
          }
          pre { background: #f2f2f2; padding: 0.8em; border-radius: 6px; overflow-x: auto; margin: 0 0 0.8em; }
          pre code { background: none; padding: 0; }
          blockquote {
            border-left: 3px solid #cccccc; margin: 0 0 0.8em; padding: 0.2em 0 0.2em 1em; color: #555555;
          }
          table { border-collapse: collapse; margin: 0 0 0.8em; width: 100%; }
          th, td { border: 1px solid #dddddd; padding: 0.4em 0.7em; text-align: left; }
          th { background: #f7f7f7; }
          img { max-width: 100%; }
          hr { border: none; border-top: 1px solid #dddddd; margin: 1.4em 0; }
          input[type="checkbox"] { margin-right: 0.4em; }
        </style>
        </head>
        <body>
        __CONTENT__
        </body>
        </html>
        """;

    public static string Build(string title, string contentHtml) =>
        Template.Replace("__TITLE__", WebUtility.HtmlEncode(title)).Replace("__CONTENT__", contentHtml);
}
