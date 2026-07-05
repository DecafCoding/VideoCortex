using Microsoft.EntityFrameworkCore;
using VideoCortex.Core.Entities;

namespace VideoCortex.Core.Db;

/// <summary>
/// EF Core context for Video Cortex. Two tables: Projects and their Videos (one-to-many).
/// The on-disk OKF-HTML libraries are the durable artifact; this store holds pipeline state.
/// </summary>
public class VideoCortexDbContext(DbContextOptions<VideoCortexDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Video> Videos => Set<Video>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(e =>
        {
            e.Property(p => p.Name).IsRequired();
            e.Property(p => p.Slug).IsRequired();
            e.HasIndex(p => p.Slug).IsUnique();

            e.HasMany(p => p.Videos)
                .WithOne(v => v.Project!)
                .HasForeignKey(v => v.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Video>(e =>
        {
            e.Property(v => v.YoutubeVideoId).IsRequired();
            // A given YouTube video appears at most once per project (no sharing across projects).
            e.HasIndex(v => new { v.ProjectId, v.YoutubeVideoId }).IsUnique();
            // Worker queues scan by pipeline stage.
            e.HasIndex(v => v.Status);
        });
    }
}
