using FileIndexProvider.Models;
using Microsoft.EntityFrameworkCore;

namespace FileIndexProvider.Database;

public sealed class FileIndexDbContext : DbContext
{
    private readonly string _dbPath;

    public DbSet<DirectoryEntity> Directories => Set<DirectoryEntity>();
    public DbSet<FileEntity> Files => Set<FileEntity>();
    public DbSet<BuildHistoryEntity> BuildHistory => Set<BuildHistoryEntity>();

    public FileIndexDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath};Cache=Shared");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DirectoryEntity>(b =>
        {
            b.ToTable("Directories");
            b.HasKey(d => d.Id);
            b.HasIndex(d => d.Path).IsUnique();
            b.HasIndex(d => d.DirectoryName);
            b.Property(d => d.Path).IsRequired();
            b.Property(d => d.DirectoryName).IsRequired();
        });

        modelBuilder.Entity<FileEntity>(b =>
        {
            b.ToTable("Files");
            b.HasKey(f => f.Id);
            b.HasIndex(f => f.FileName);
            b.HasIndex(f => f.Extension);
            b.HasIndex(f => f.DirectoryId);
            b.HasOne(f => f.Directory)
             .WithMany(d => d.Files)
             .HasForeignKey(f => f.DirectoryId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BuildHistoryEntity>(b =>
        {
            b.ToTable("BuildHistory");
            b.HasKey(h => h.Id);
            b.HasIndex(h => new { h.Status, h.Id });
        });
    }
}
