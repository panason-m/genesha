using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Genesha.Helpers;

namespace Genesha.Views;

public sealed partial class UserManualPage : Page
{
    public UserManualPage()
    {
        InitializeComponent();

        ContentPanel.Children.Add(MarkdownRenderer.Render(IntroText));

        AddSection("Workspaces", WorkspacesText, expanded: true);
        AddSection("Notes", NotesText);
        AddSection("Note Types", NoteTypesText);
        AddSection("Whiteboards", WhiteboardsText);
        AddSection("Mermaid Charts", MermaidChartsText);
        AddSection("Exporting", ExportingText);
        AddSection("Your data", DataText);
    }

    private void AddSection(string title, string markdown, bool expanded = false)
    {
        var expander = new Expander
        {
            Header = title,
            Content = MarkdownRenderer.Render(markdown),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsExpanded = expanded,
            Margin = new Thickness(0, 0, 0, 8),
        };
        ContentPanel.Children.Add(expander);
    }

    private const string IntroText = @"# User manual

Genesha is a single-user, local app for Notion-style notes and Miro-style whiteboards, organized
into **workspaces**. Everything you create is stored on this computer only, inside the workspace
folder you choose — nothing is uploaded anywhere.";

    private const string WorkspacesText = @"- A workspace is just a folder on disk. Click **New Workspace** on the Workspaces screen, pick a parent folder, and give it a name — Genesha creates the folder for you.
- Already have a Genesha workspace folder (e.g. shared from another computer)? Click **Open Existing Folder** and point at it directly.
- Use the **…** menu on a workspace tile to *Rename* (this only changes the display name, not the folder itself) or *Remove* it from the list. Removing gives you the option to also delete the folder from disk.
- Click a workspace to open it — you'll land on its **Dashboard**, showing note and whiteboard counts with quick links into each.";

    private const string NotesText = @"- From the Dashboard or the **Notes** tab, click **New Note** to create one, optionally assigning it a type.
- Click a note to open the block editor: type `/` for a menu of block types (headings, lists, and more), same as Notion.
- Content saves automatically as you type — no save button needed.
- Use the search box to filter notes by name, and the type dropdown to narrow the list down to a single type.
- Use a note's **…** menu to *Rename*, *Change Type*, or *Delete* it.
- In the note editor, use **Export MD** or **Export PDF** to save a copy outside the workspace — see *Exporting* below.";

    private const string NoteTypesText = @"- Note Types are a simple tag you can put on a note — useful for grouping notes (e.g. *Meeting Notes*, *Ideas*, *Reference*).
- Open **Note Types** from the sidebar to add, rename, or delete types.
- Deleting a type doesn't delete its notes — they just become untyped.";

    private const string WhiteboardsText = @"- From the Dashboard or the **Whiteboards** tab, click **New Whiteboard** to create one.
- Click a whiteboard to open the canvas: an infinite drawing surface with shapes, lines/connectors, freehand drawing, text, and sticky notes, in the same style as Miro or Canva.
- Drawing saves automatically as you go.
- Select a group of shapes and arrows and use the right-click menu to *Export as PNG* (saves an image — see *Exporting* below) or *Export as Mermaid Flow* (creates a new item in **Mermaid Charts** from the shapes and connectors).
- Use a whiteboard's **…** menu to *Rename* or *Delete* it.";

    private const string MermaidChartsText = @"- From the Dashboard or the **Mermaid Charts** tab, click **New Chart** to create one, or generate one from a whiteboard flow group (see *Whiteboards* above).
- Edit the diagram source on the left using [Mermaid](https://mermaid.js.org) syntax (flowcharts, sequence diagrams, and more); the rendered preview on the right updates live.
- Drag nodes or subgraphs in the preview to nudge their layout; **Reset layout** undoes that.
- Content saves automatically as you edit.
- Use **Export PNG** in the preview toolbar to save the rendered diagram as an image — see *Exporting* below.";

    private const string ExportingText = @"Notes, Mermaid diagrams, and whiteboard selections can all be exported to a file outside the workspace:

- **Notes** — *Export MD* saves the raw markdown; *Export PDF* renders a clean, print-styled document (not a screenshot of the editor).
- **Mermaid Charts** — *Export PNG* in the preview toolbar saves the rendered diagram as an image.
- **Whiteboards** — select a group and choose *Export as PNG* from the right-click menu.

Every export opens the native **Save As** dialog, so you choose the file name and folder each time — nothing is written automatically. Each export type remembers its own last-used folder.";

    private const string DataText = @"Each workspace folder is self-contained and portable:

- **.genesha\genesha.db** — the database with note, whiteboard, and diagram metadata (names, types, timestamps).
- **Notes\** — one markdown file per note.
- **Whiteboards\** — one snapshot file per whiteboard.
- **MermaidCharts\** — one source file per diagram.

The list of workspaces you've created or opened is remembered separately, under
**%LOCALAPPDATA%\Genesha\workspaces.json**.

Exported files (Markdown, PDF, PNG) are *not* part of the workspace — you choose their location yourself each time via the Save As dialog, so they can live anywhere on disk.";
}
