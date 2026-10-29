using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Shared.Common.Common;

namespace Project.Infrastructure.Persistence;

public sealed class ProjectContext(DbContextOptions<ProjectContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<LandPlot> LandPlots => Set<LandPlot>();
    public DbSet<LandscapeProject> Projects => Set<LandscapeProject>();
    public DbSet<ProjectStage> Stages => Set<ProjectStage>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();
    public DbSet<Estimate> Estimates => Set<Estimate>();
    public DbSet<EstimateLineItem> EstimateLineItems => Set<EstimateLineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureEntity<Client>(modelBuilder);
        ConfigureEntity<LandPlot>(modelBuilder);
        ConfigureEntity<LandscapeProject>(modelBuilder);
        ConfigureEntity<ProjectStage>(modelBuilder);
        ConfigureEntity<ProjectTask>(modelBuilder);
        ConfigureEntity<Estimate>(modelBuilder);
        ConfigureEntity<EstimateLineItem>(modelBuilder);

        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("clients");
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.BillingAddress).HasMaxLength(500);
            entity.HasIndex(x => x.DisplayName);
            entity.HasIndex(x => x.Email);
        });
        modelBuilder.Entity<LandPlot>(entity =>
        {
            entity.ToTable("land_plots");
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(500).IsRequired();
            entity.Property(x => x.AreaSquareMetres).HasPrecision(18, 2);
            entity.Property(x => x.SoilType).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.HasIndex(x => x.Name);
        });
        modelBuilder.Entity<LandscapeProject>(entity =>
        {
            entity.ToTable("projects");
            entity.Property(x => x.ProjectNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(x => x.ProjectNumber).IsUnique();
            entity.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LandPlot>().WithMany().HasForeignKey(x => x.LandPlotId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Stages).WithOne().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ProjectStage>(entity =>
        {
            entity.ToTable("project_stages");
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(x => new { x.ProjectId, x.Sequence }).IsUnique();
            entity.HasAlternateKey(x => new { x.ProjectId, x.Id });
        });
        modelBuilder.Entity<ProjectTask>(entity =>
        {
            entity.ToTable("project_tasks");
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasOne<LandscapeProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ProjectStage>().WithMany(x => x.Tasks)
                .HasForeignKey(x => new { x.ProjectId, x.StageId })
                .HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.ProjectId, x.Status });
            entity.HasIndex(x => x.DueDate);
        });
        modelBuilder.Entity<Estimate>(entity =>
        {
            entity.ToTable("estimates");
            entity.Ignore(x => x.EstimatedTotal);
            entity.Ignore(x => x.EstimatedCost);
            entity.Property(x => x.Version).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasOne<LandscapeProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.ProjectId, x.Version }).IsUnique();
            entity.HasMany(x => x.LineItems).WithOne().HasForeignKey(x => x.EstimateId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<EstimateLineItem>(entity =>
        {
            entity.ToTable("estimate_line_items");
            entity.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitSymbol).HasMaxLength(24).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
        });
    }

    private static void ConfigureEntity<TEntity>(ModelBuilder modelBuilder) where TEntity : EntityBase
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedDate).IsRequired();
            entity.Property(x => x.LastModifiedDate);
        });
    }

    private void StampAuditFields()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedDate == default)
                entry.Entity.CreatedDate = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.LastModifiedDate = now;
        }
    }
    public override int SaveChanges() { StampAuditFields(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }
}
