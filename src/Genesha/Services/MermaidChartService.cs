using Microsoft.EntityFrameworkCore;
using Genesha.Data;
using Genesha.Models;

namespace Genesha.Services;

public record MermaidChartListItem(int Id, string Name, string? SourceWhiteboardName, DateTime UpdatedAt);

public class MermaidChartService
{
    private readonly IDbContextFactory<WorkspaceDbContext> _factory;
    private readonly CurrentWorkspaceService _currentWorkspace;

    public MermaidChartService(IDbContextFactory<WorkspaceDbContext> factory, CurrentWorkspaceService currentWorkspace)
    {
        _factory = factory;
        _currentWorkspace = currentWorkspace;
    }

    public async Task<List<MermaidChartListItem>> GetMermaidChartsAsync(string? searchText = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.MermaidCharts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
            query = query.Where(m => EF.Functions.Like(m.Name, $"%{searchText}%"));

        var charts = await query.OrderByDescending(m => m.UpdatedAt).ToListAsync();
        return charts.Select(m => new MermaidChartListItem(m.Id, m.Name, m.SourceWhiteboardName, m.UpdatedAt)).ToList();
    }

    public async Task<int> GetMermaidChartCountAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.MermaidCharts.CountAsync();
    }

    public async Task<MermaidChart> CreateMermaidChartAsync(string name, string initialText, string? sourceWhiteboardName = null)
    {
        var relativePath = Path.Combine("MermaidCharts", $"{Guid.NewGuid():N}.mmd");
        var absolutePath = ResolveAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllTextAsync(absolutePath, initialText);

        var now = DateTime.Now;
        var chart = new MermaidChart
        {
            Name = name,
            RelativeFilePath = relativePath,
            SourceWhiteboardName = sourceWhiteboardName,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await using var db = await _factory.CreateDbContextAsync();
        db.MermaidCharts.Add(chart);
        await db.SaveChangesAsync();
        return chart;
    }

    public async Task<string> LoadMermaidChartTextAsync(int chartId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var chart = await db.MermaidCharts.FindAsync(chartId)
            ?? throw new InvalidOperationException($"Mermaid Chart {chartId} not found.");
        var absolutePath = ResolveAbsolutePath(chart.RelativeFilePath);
        return File.Exists(absolutePath) ? await File.ReadAllTextAsync(absolutePath) : "";
    }

    public async Task SaveMermaidChartTextAsync(int chartId, string mermaidText)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var chart = await db.MermaidCharts.FindAsync(chartId)
            ?? throw new InvalidOperationException($"Mermaid Chart {chartId} not found.");
        var absolutePath = ResolveAbsolutePath(chart.RelativeFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllTextAsync(absolutePath, mermaidText);

        chart.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public async Task RenameMermaidChartAsync(int chartId, string newName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var chart = await db.MermaidCharts.FindAsync(chartId);
        if (chart is null) return;
        chart.Name = newName;
        chart.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public async Task DeleteMermaidChartAsync(int chartId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var chart = await db.MermaidCharts.FindAsync(chartId);
        if (chart is null) return;

        var absolutePath = ResolveAbsolutePath(chart.RelativeFilePath);
        db.MermaidCharts.Remove(chart);
        await db.SaveChangesAsync();

        if (File.Exists(absolutePath)) File.Delete(absolutePath);
    }

    private string ResolveAbsolutePath(string relativePath) =>
        Path.Combine(_currentWorkspace.RequireFolderPath(), relativePath);
}
