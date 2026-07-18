using Microsoft.EntityFrameworkCore;
using NodeReel.Domain.Entities;

namespace NodeReel.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<NodeProvider> NodeProviders => Set<NodeProvider>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<RunStep> RunSteps => Set<RunStep>();
    public DbSet<MediaObject> MediaObjects => Set<MediaObject>();
    public DbSet<Workflow> Workflows => Set<Workflow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<NodeProvider>(e =>
        {
            e.ToTable("node_providers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.BaseUrl).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<PipelineRun>(e =>
        {
            e.ToTable("pipeline_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.GraphJson).IsRequired();
            e.HasIndex(x => x.UserId);
            e.HasMany(x => x.Steps)
                .WithOne(x => x.PipelineRun)
                .HasForeignKey(x => x.PipelineRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RunStep>(e =>
        {
            e.ToTable("run_steps");
            e.HasKey(x => x.Id);
            // Client-generated Guids: without this, EF treats a non-default Id as "already exists"
            // and issues UPDATE instead of INSERT → DbUpdateConcurrencyException (0 rows).
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.NodeInstanceId).HasMaxLength(100).IsRequired();
            e.Property(x => x.NodeTypeId).HasMaxLength(200).IsRequired();
            e.Property(x => x.ProviderId).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<MediaObject>(e =>
        {
            e.ToTable("media_objects");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.ObjectKey).IsUnique();
            e.HasIndex(x => x.UserId);
            e.Property(x => x.ObjectKey).HasMaxLength(500).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Workflow>(e =>
        {
            e.ToTable("workflows");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.GraphJson).IsRequired();
            e.HasIndex(x => x.UserId);
        });
    }
}
