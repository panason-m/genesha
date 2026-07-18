using Microsoft.EntityFrameworkCore;
using Genesha.Data;
using Genesha.Models;

namespace Genesha.Services;

public record NoteListItem(int Id, string Name, DateTime UpdatedAt, int? NoteTypeId, string? NoteTypeName);

public class NoteService
{
    private readonly IDbContextFactory<WorkspaceDbContext> _factory;
    private readonly CurrentWorkspaceService _currentWorkspace;

    public NoteService(IDbContextFactory<WorkspaceDbContext> factory, CurrentWorkspaceService currentWorkspace)
    {
        _factory = factory;
        _currentWorkspace = currentWorkspace;
    }

    public async Task<List<NoteListItem>> GetNotesAsync(string? searchText = null, int? noteTypeId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.Notes.Include(n => n.NoteType).AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
            query = query.Where(n => EF.Functions.Like(n.Name, $"%{searchText}%"));

        if (noteTypeId is not null)
            query = query.Where(n => n.NoteTypeId == noteTypeId);

        var notes = await query.OrderByDescending(n => n.UpdatedAt).ToListAsync();
        return notes.Select(n => new NoteListItem(n.Id, n.Name, n.UpdatedAt, n.NoteTypeId, n.NoteType?.Name)).ToList();
    }

    public async Task<int> GetNoteCountAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Notes.CountAsync();
    }

    public async Task<Note> CreateNoteAsync(string name, int? noteTypeId = null)
    {
        var relativePath = Path.Combine("Notes", $"{Guid.NewGuid():N}.md");
        var absolutePath = ResolveAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllTextAsync(absolutePath, "");

        var now = DateTime.Now;
        var note = new Note
        {
            Name = name,
            RelativeFilePath = relativePath,
            CreatedAt = now,
            UpdatedAt = now,
            NoteTypeId = noteTypeId,
        };

        await using var db = await _factory.CreateDbContextAsync();
        db.Notes.Add(note);
        await db.SaveChangesAsync();
        return note;
    }

    public async Task<string> LoadNoteContentAsync(int noteId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var note = await db.Notes.FindAsync(noteId) ?? throw new InvalidOperationException($"Note {noteId} not found.");
        var absolutePath = ResolveAbsolutePath(note.RelativeFilePath);
        return File.Exists(absolutePath) ? await File.ReadAllTextAsync(absolutePath) : "";
    }

    public async Task SaveNoteContentAsync(int noteId, string markdown)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var note = await db.Notes.FindAsync(noteId) ?? throw new InvalidOperationException($"Note {noteId} not found.");
        var absolutePath = ResolveAbsolutePath(note.RelativeFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllTextAsync(absolutePath, markdown);

        note.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public async Task RenameNoteAsync(int noteId, string newName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var note = await db.Notes.FindAsync(noteId);
        if (note is null) return;
        note.Name = newName;
        note.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public async Task SetNoteTypeAsync(int noteId, int? noteTypeId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var note = await db.Notes.FindAsync(noteId);
        if (note is null) return;
        note.NoteTypeId = noteTypeId;
        note.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public async Task DeleteNoteAsync(int noteId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var note = await db.Notes.FindAsync(noteId);
        if (note is null) return;

        var absolutePath = ResolveAbsolutePath(note.RelativeFilePath);
        db.Notes.Remove(note);
        await db.SaveChangesAsync();

        if (File.Exists(absolutePath)) File.Delete(absolutePath);
    }

    private string ResolveAbsolutePath(string relativePath) =>
        Path.Combine(_currentWorkspace.RequireFolderPath(), relativePath);
}
