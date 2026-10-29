using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Common;

namespace Catalog.Infrastructure.Persistence;

public sealed class CatalogContext : DbContext
{
    public CatalogContext(DbContextOptions<CatalogContext> options) : base(options) { }

    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<WorkType> WorkTypes => Set<WorkType>();
    public DbSet<MeasurementUnit> MeasurementUnits => Set<MeasurementUnit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureEntity<MeasurementUnit>(modelBuilder);
        ConfigureEntity<Plant>(modelBuilder);
        ConfigureEntity<Material>(modelBuilder);
        ConfigureEntity<WorkType>(modelBuilder);

        modelBuilder.Entity<MeasurementUnit>(entity =>
        {
            entity.ToTable("measurement_units");
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Symbol).HasMaxLength(24).IsRequired();
            entity.HasIndex(x => x.Symbol).IsUnique();
            entity.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<Plant>(entity =>
        {
            entity.ToTable("plants");
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.LatinName).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.HasIndex(x => x.Name);
            entity.HasOne<MeasurementUnit>()
                .WithMany()
                .HasForeignKey(x => x.MeasurementUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Material>(entity =>
        {
            entity.ToTable("materials");
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.HasIndex(x => x.Sku).IsUnique();
            entity.HasIndex(x => x.Name);
            entity.HasOne<MeasurementUnit>()
                .WithMany()
                .HasForeignKey(x => x.MeasurementUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkType>(entity =>
        {
            entity.ToTable("work_types");
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.BasePrice).HasPrecision(18, 2);
            entity.HasIndex(x => x.Name);
            entity.HasOne<MeasurementUnit>()
                .WithMany()
                .HasForeignKey(x => x.MeasurementUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureEntity<TEntity>(ModelBuilder modelBuilder)
        where TEntity : EntityBase
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedDate).IsRequired();
            entity.Property(x => x.LastModifiedDate);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedDate == default)
                entry.Entity.CreatedDate = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.LastModifiedDate = now;
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedDate == default)
                entry.Entity.CreatedDate = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.LastModifiedDate = now;
        }

        return base.SaveChanges();
    }
}
