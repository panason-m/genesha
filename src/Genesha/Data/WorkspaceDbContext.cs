using Microsoft.EntityFrameworkCore;
using Genesha.Models;

namespace Genesha.Data;

public class WorkspaceDbContext : DbContext
{
    public WorkspaceDbContext(DbContextOptions<WorkspaceDbContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteType> NoteTypes => Set<NoteType>();
    public DbSet<Whiteboard> Whiteboards => Set<Whiteboard>();
    public DbSet<MermaidChart> MermaidCharts => Set<MermaidChart>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>()
            .HasIndex(n => n.UpdatedAt);
        modelBuilder.Entity<Note>()
            .HasIndex(n => n.Name);

        modelBuilder.Entity<NoteType>()
            .HasMany(nt => nt.Notes).WithOne(n => n.NoteType)
            .HasForeignKey(n => n.NoteTypeId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Whiteboard>()
            .HasIndex(w => w.UpdatedAt);
        modelBuilder.Entity<Whiteboard>()
            .HasIndex(w => w.Name);

        modelBuilder.Entity<MermaidChart>()
            .HasIndex(m => m.UpdatedAt);
        modelBuilder.Entity<MermaidChart>()
            .HasIndex(m => m.Name);
    }
}
