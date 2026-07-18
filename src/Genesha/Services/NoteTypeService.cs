using Microsoft.EntityFrameworkCore;
using Genesha.Data;
using Genesha.Models;

namespace Genesha.Services;

public class NoteTypeService
{
    private readonly IDbContextFactory<WorkspaceDbContext> _factory;

    public NoteTypeService(IDbContextFactory<WorkspaceDbContext> factory) => _factory = factory;

    public async Task<List<NoteType>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.NoteTypes.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync();
    }

    public async Task<NoteType> CreateAsync(string name)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var maxSortOrder = await db.NoteTypes.Select(t => (int?)t.SortOrder).MaxAsync() ?? 0;
        var type = new NoteType { Name = name, SortOrder = maxSortOrder + 1024 };
        db.NoteTypes.Add(type);
        await db.SaveChangesAsync();
        return type;
    }

    public async Task RenameAsync(int id, string newName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var type = await db.NoteTypes.FindAsync(id);
        if (type is null) return;
        type.Name = newName;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var type = await db.NoteTypes.FindAsync(id);
        if (type is null) return;
        db.NoteTypes.Remove(type);
        await db.SaveChangesAsync();
    }
}
