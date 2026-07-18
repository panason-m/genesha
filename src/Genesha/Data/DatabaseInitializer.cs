using Microsoft.EntityFrameworkCore;

namespace Genesha.Data;

public static class DatabaseInitializer
{
    /// <summary>Creates the workspace's schema if needed. Called each time a workspace is opened,
    /// since (unlike Tossakan) there's no single database to initialize once at app startup.</summary>
    public static async Task InitializeAsync(IDbContextFactory<WorkspaceDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        await ApplySchemaDriftAsync(db);
    }

    /// <summary>
    /// EnsureCreatedAsync only builds the schema for a brand-new database file, so tables added in
    /// later Genesha versions (e.g. Whiteboards, MermaidCharts, added after the first release) need
    /// to be created by hand for workspaces whose .genesha\genesha.db predates them, following the
    /// same check-then-ALTER pattern Tossakan uses for its BackgroundImagePath column.
    /// </summary>
    private static async Task ApplySchemaDriftAsync(WorkspaceDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync();
        try
        {
            bool hasWhiteboardsTable;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Whiteboards'";
                hasWhiteboardsTable = await cmd.ExecuteScalarAsync() is not null;
            }

            if (!hasWhiteboardsTable)
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TABLE "Whiteboards" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_Whiteboards" PRIMARY KEY AUTOINCREMENT,
                        "Name" TEXT NOT NULL,
                        "RelativeFilePath" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NOT NULL
                    )
                    """);
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX \"IX_Whiteboards_UpdatedAt\" ON \"Whiteboards\" (\"UpdatedAt\")");
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX \"IX_Whiteboards_Name\" ON \"Whiteboards\" (\"Name\")");
            }

            bool hasMermaidChartsTable;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='MermaidCharts'";
                hasMermaidChartsTable = await cmd.ExecuteScalarAsync() is not null;
            }

            if (!hasMermaidChartsTable)
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TABLE "MermaidCharts" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_MermaidCharts" PRIMARY KEY AUTOINCREMENT,
                        "Name" TEXT NOT NULL,
                        "RelativeFilePath" TEXT NOT NULL,
                        "SourceWhiteboardName" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NOT NULL
                    )
                    """);
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX \"IX_MermaidCharts_UpdatedAt\" ON \"MermaidCharts\" (\"UpdatedAt\")");
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX \"IX_MermaidCharts_Name\" ON \"MermaidCharts\" (\"Name\")");
            }
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
    }
}
