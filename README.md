# Genesha

A free, open source notes and whiteboard app for Windows, in the spirit of Notion and Miro.
Built with WinUI 3 and .NET 8. No account, no cloud, no subscription. Every workspace is just a
folder on your own disk.

![Platform](https://img.shields.io/badge/platform-Windows-0078D6)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)
![Status](https://img.shields.io/badge/status-v1.0%20Beta-orange)

## Why this exists

Notion and Miro are great, and both require an account and a network connection to a server you
don't control. Genesha is a native Windows app that does the two things people reach for those
tools for most (block based notes, and an infinite whiteboard canvas) entirely offline. A
workspace is a plain folder: markdown files for notes, JSON snapshots for whiteboards, one small
SQLite database for metadata. Copy the folder, move it, back it up with whatever tool you already
use. It is free to use, free to fork, and contributions are welcome.

## What it does

- **Workspaces**: each workspace is a folder you choose on disk. Create a new one via a folder
  picker, or open an existing one; rename or remove workspaces from the list
- **Notes**: Notion style block editing (headings, lists, bold/italic, slash command menu, and
  more) via an embedded BlockNote editor. Notes have a name, timestamps, and an optional user
  defined type. Search and filter by name and type
- **Note Types**: a dedicated settings page to add, rename, or delete note types
- **Whiteboards**: an infinite drawing canvas (shapes, connectors, freehand drawing, text, sticky
  notes) via an embedded tldraw canvas, in the same style as Miro or Canva
- **Dashboard**: a per workspace landing page showing note/whiteboard counts and quick links
- **User Manual**: in-app help, always reachable from the nav pane footer

## Compatibility

Genesha targets Windows only, via WinUI 3 / the Windows App SDK. There is no macOS or Linux
build, and none is planned since the UI framework itself is Windows specific.

- Windows 10, build 19041 or later, or Windows 11
- x64 only
- Requires the WebView2 runtime, which ships with Windows 11 and current Windows 10 by default
- Standalone published builds are self contained: they bundle the .NET runtime and the Windows
  App SDK, so they run on a machine with nothing else installed

## Tech stack

| Layer | Choice |
|---|---|
| UI framework | WinUI 3 (Windows App SDK 1.8.x), unpackaged desktop app |
| Language/runtime | C#, .NET 8 (`net8.0-windows10.0.19041.0`) |
| MVVM helpers | CommunityToolkit.Mvvm |
| Data access | EF Core 8 + SQLite, one database per workspace |
| Rich editors | WebView2 hosting two small Vite + React bundles: BlockNote for notes, tldraw for whiteboards, talking to native code via postMessage |

Project layout:

```
src/Genesha/
  Models/                  Entities.cs (Note, NoteType, Whiteboard), WorkspaceRegistryEntry
  Data/                    WorkspaceDbContext, WorkspaceDbContextFactory, DatabaseInitializer
  Services/                WorkspaceRegistryService, CurrentWorkspaceService, NoteService,
                           NoteTypeService, WhiteboardService, AppPaths
  ViewModels/              Small row view models for list/settings pages
  Views/                   WorkspacePage, WorkspaceDashboardPage, NoteListPage,
                           NoteTypeSettingsPage, NoteEditorPage, WhiteboardListPage,
                           WhiteboardEditorPage, UserManualPage
  wwwroot/                 Built output of the web/ editors (WebView2 serves from here)
  Assets/                  App icon and source artwork
web/
  blocknote-editor/        Standalone Vite + React project wrapping BlockNote
  tldraw-editor/           Standalone Vite + React project wrapping tldraw
```

Everything is built with the `dotnet` CLI. No Visual Studio install is required.

## Getting started

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64), Windows 10
19041+ or Windows 11. The repository ships the web editors already built into
`src/Genesha/wwwroot`, so Node.js is only needed if you plan to modify the editors themselves.

```
git clone https://github.com/panason-m/genesha.git
cd genesha
dotnet run --project "src\Genesha"
```

If you change anything under `web\blocknote-editor` or `web\tldraw-editor`, rebuild the bundles
before running, since the compiled output in `src\Genesha\wwwroot` is what WebView2 actually
loads:

```
cd web\blocknote-editor && npm install && npm run build
cd ..\tldraw-editor && npm install && npm run build
```

## Building and publishing a release build

A standalone build bundles the .NET runtime and the Windows App SDK so it runs on a machine with
nothing else installed. No installer, just a folder you run or zip up.

One step publish (recommended):

```
powershell -ExecutionPolicy Bypass -File .\publish.ps1
```

This runs `dotnet publish` in Release mode for `win-x64`, self contained, into `.\publish\`, and
creates a desktop shortcut pointing at `publish\Genesha.exe`.

Manual publish, equivalent without the shortcut step:

```
dotnet publish "src\Genesha\Genesha.csproj" -c Release -r win-x64 --self-contained true -o publish
```

To distribute the build, copy the entire `publish\` folder, not just the exe, since it depends on
sibling `.dll`/`.pri` files and language resource folders next to it. There is no installer; it
runs directly from that folder.

Re-publishing after changes is just re-running the same command; it overwrites `publish\` in
place.

## Where your data lives

Everything is stored locally, never sent anywhere:

- **Workspace registry** (the list of known workspaces): `%LOCALAPPDATA%\Genesha\workspaces.json`
- **Per workspace data**, inside whatever folder you chose for that workspace:
  - `.genesha\genesha.db`: SQLite database with note/whiteboard metadata
  - `Notes\*.md`: one markdown file per note
  - `Whiteboards\*.json`: one tldraw snapshot per whiteboard

A workspace folder is self contained and portable. Copy or move it anywhere, then open it again
via Open Existing Folder.

## Letting your local AI agent recognize Genesha

Genesha has no API on purpose. All state lives in plain files and one small SQLite database per
workspace, which means any local agentic AI tool you already run (Claude Code, Cursor, or
anything else with file/shell access) can read and write your notes and workspaces directly, with
no server, no auth, and no SDK to install.

The app is usually running while your agent works, so keep connections short lived: open, do one
read or one write inside a transaction, close. Don't hold a connection open across multiple tool
calls.

Layout, in brief:

- Workspace registry (name to folder path): `%LOCALAPPDATA%\Genesha\workspaces.json`
- Per workspace database: `<workspace>\.genesha\genesha.db`, table `Notes` (Id, Name,
  RelativeFilePath, CreatedAt, UpdatedAt, NoteTypeId)
- Note content itself: plain markdown files under `<workspace>\Notes\<guid>.md`, one file per row
  in the `Notes` table
- To add a note by hand: write the `.md` file first, then insert a matching row into `Notes`

To make your own agent aware of this, drop something like the following into whatever file your
tool reads for standing instructions (`CLAUDE.md`, `AGENTS.md`, `.cursorrules`, etc.):

```
Genesha is a local notes and whiteboard app. Workspaces are registered by name in
%LOCALAPPDATA%\Genesha\workspaces.json, each pointing at a folder on disk. Each
workspace has its own SQLite database at <workspace>\.genesha\genesha.db (table
Notes: Id, Name, RelativeFilePath, CreatedAt, UpdatedAt, NoteTypeId) and stores note
content as markdown files under <workspace>\Notes\<guid>.md. Read or write the
database with any SQLite client (the `sqlite3` CLI, DB Browser for SQLite, or a
couple lines of Python's built in sqlite3 module all work). Use short lived
connections and transactions since the app itself may be running at the same time.
To add a note: write the markdown file, then insert a matching row into Notes.
```

## Contributing

Issues and pull requests are welcome. The native side is a fairly standard MVVM app; the two
embedded editors under `web/` are small, independent Vite + React projects, so changes there
don't require touching any C#.

## License

MIT, see [LICENSE](LICENSE). Use it, fork it, ship your own version, no permission needed.
