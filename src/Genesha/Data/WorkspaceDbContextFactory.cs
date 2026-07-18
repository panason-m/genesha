using Microsoft.EntityFrameworkCore;
using Genesha.Services;

namespace Genesha.Data;

/// <summary>
/// Unlike Tossakan's AppDbContext (registered once against a fixed path), Genesha's database
/// location depends on whichever workspace is currently open, and can change mid-session. This
/// factory reads the active path from CurrentWorkspaceService on every call instead of capturing
/// a fixed connection string at startup.
/// </summary>
public class WorkspaceDbContextFactory : IDbContextFactory<WorkspaceDbContext>
{
    private readonly CurrentWorkspaceService _currentWorkspace;

    public WorkspaceDbContextFactory(CurrentWorkspaceService currentWorkspace) => _currentWorkspace = currentWorkspace;

    public WorkspaceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkspaceDbContext>()
            .UseSqlite($"Data Source={_currentWorkspace.DbPath}")
            .Options;
        return new WorkspaceDbContext(options);
    }

    public Task<WorkspaceDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}
