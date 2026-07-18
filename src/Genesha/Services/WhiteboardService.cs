using Microsoft.EntityFrameworkCore;
using Genesha.Data;
using Genesha.Models;

namespace Genesha.Services;

public record WhiteboardListItem(int Id, string Name, DateTime UpdatedAt);

public class WhiteboardService
{
    private readonly IDbContextFactory<WorkspaceDbContext> _factory;
    private readonly CurrentWorkspaceService _currentWorkspace;

    public WhiteboardService(IDbContextFactory<WorkspaceDbContext> factory, CurrentWorkspaceService currentWorkspace)
    {
        _factory = factory;
        _currentWorkspace = currentWorkspace;
    }

    public async Task<List<WhiteboardListItem>> GetWhiteboardsAsync(string? searchText = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.Whiteboards.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
            query = query.Where(w => EF.Functions.Like(w.Name, $"%{searchText}%"));

        var boards = await query.OrderByDescending(w => w.UpdatedAt).ToListAsync();
        return boards.Select(w => new WhiteboardListItem(w.Id, w.Name, w.UpdatedAt)).ToList();
    }

    public async Task<int> GetWhiteboardCountAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Whiteboards.CountAsync();
    }

    public async Task<Whiteboard> CreateWhiteboardAsync(string name)
    {
        var relativePath = Path.Combine("Whiteboards", $"{Guid.NewGuid():N}.json");
        var absolutePath = ResolveAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllTextAsync(absolutePath, "");

        var now = DateTime.Now;
        var board = new Whiteboard
        {
            Name = name,
            RelativeFilePath = relativePath,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await using var db = await _factory.CreateDbContextAsync();
        db.Whiteboards.Add(board);
        await db.SaveChangesAsync();
        return board;
    }

    public async Task<string> LoadWhiteboardSnapshotAsync(int whiteboardId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var board = await db.Whiteboards.FindAsync(whiteboardId)
            ?? throw new InvalidOperationException($"Whiteboard {whiteboardId} not found.");
        var absolutePath = ResolveAbsolutePath(board.RelativeFilePath);
        return File.Exists(absolutePath) ? await File.ReadAllTextAsync(absolutePath) : "";
    }

    public async Task SaveWhiteboardSnapshotAsync(int whiteboardId, string snapshotJson)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var board = await db.Whiteboards.FindAsync(whiteboardId)
            ?? throw new InvalidOperationException($"Whiteboard {whiteboardId} not found.");
        var absolutePath = ResolveAbsolutePath(board.RelativeFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllTextAsync(absolutePath, snapshotJson);

        board.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public async Task RenameWhiteboardAsync(int whiteboardId, string newName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var board = await db.Whiteboards.FindAsync(whiteboardId);
        if (board is null) return;
        board.Name = newName;
        board.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public async Task DeleteWhiteboardAsync(int whiteboardId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var board = await db.Whiteboards.FindAsync(whiteboardId);
        if (board is null) return;

        var absolutePath = ResolveAbsolutePath(board.RelativeFilePath);
        db.Whiteboards.Remove(board);
        await db.SaveChangesAsync();

        if (File.Exists(absolutePath)) File.Delete(absolutePath);
    }

    public async Task<string> ExportGroupPngAsync(string boardName, byte[] pngBytes)
    {
        var exportsDir = Path.Combine(_currentWorkspace.RequireFolderPath(), "Exports");
        Directory.CreateDirectory(exportsDir);

        var baseFileName = $"{SanitizeFileNameComponent(boardName)}-{DateTime.Now:yyyyMMdd-HHmmss}";
        var fileName = $"{baseFileName}.png";
        var counter = 2;
        while (File.Exists(Path.Combine(exportsDir, fileName)))
        {
            fileName = $"{baseFileName}-{counter}.png";
            counter++;
        }

        await File.WriteAllBytesAsync(Path.Combine(exportsDir, fileName), pngBytes);
        return fileName;
    }

    private static string SanitizeFileNameComponent(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Whiteboard" : sanitized;
    }

    private string ResolveAbsolutePath(string relativePath) =>
        Path.Combine(_currentWorkspace.RequireFolderPath(), relativePath);
}
