using Microsoft.EntityFrameworkCore;
using Procurement.Domain.Entities;
using Shared.Common.Common;

namespace Procurement.Infrastructure.Persistence;

public sealed class ProcurementContext(DbContextOptions<ProcurementContext> options) : DbContext(options)
{
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<SupplierDelivery> Deliveries => Set<SupplierDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureEntity<Supplier>(modelBuilder); ConfigureEntity<PurchaseOrder>(modelBuilder);
        ConfigureEntity<PurchaseOrderLine>(modelBuilder); ConfigureEntity<StockItem>(modelBuilder);
        ConfigureEntity<StockMovement>(modelBuilder); ConfigureEntity<SupplierDelivery>(modelBuilder);

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ContactName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.TaxIdentifier).HasMaxLength(80);
            entity.Property(x => x.Address).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Name);
        });
        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("purchase_orders");
            entity.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Notes).HasMaxLength(4000).IsRequired();
            entity.Ignore(x => x.OrderTotal);
            entity.HasIndex(x => x.OrderNumber).IsUnique();
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Lines).WithOne(x => x.PurchaseOrder).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.ToTable("purchase_order_lines");
            entity.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.UnitSymbol).HasMaxLength(24).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.ReceivedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Ignore(x => x.LineTotal);
        });
        modelBuilder.Entity<StockItem>(entity =>
        {
            entity.ToTable("stock_items");
            entity.Property(x => x.Sku).HasMaxLength(48).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitSymbol).HasMaxLength(24).IsRequired();
            entity.Property(x => x.StorageLocation).HasMaxLength(100).IsRequired();
            entity.Property(x => x.QuantityOnHand).HasPrecision(18, 3);
            entity.Property(x => x.ReorderLevel).HasPrecision(18, 3);
            entity.Ignore(x => x.AvailableQuantity);
            entity.Ignore(x => x.IsBelowReorderLevel);
            entity.HasIndex(x => x.Sku).IsUnique();
            entity.HasIndex(x => new { x.QuantityOnHand, x.ReorderLevel });
        });
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements");
            entity.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.SourceDocument).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000).IsRequired();
            entity.HasOne(x => x.StockItem).WithMany().HasForeignKey(x => x.StockItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.StockItemId, x.CreatedDate });
        });
        modelBuilder.Entity<SupplierDelivery>(entity =>
        {
            entity.ToTable("supplier_deliveries");
            entity.Property(x => x.DeliveryNumber).HasMaxLength(48).IsRequired();
            entity.Property(x => x.Carrier).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TrackingReference).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.DeliveryNumber).IsUnique();
            entity.HasIndex(x => new { x.PurchaseOrderId, x.Status });
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
            if (entry.State == EntityState.Added && entry.Entity.CreatedDate == default) entry.Entity.CreatedDate = now;
            else if (entry.State == EntityState.Modified) entry.Entity.LastModifiedDate = now;
        }
    }
    public override int SaveChanges() { StampAuditFields(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields(); return base.SaveChangesAsync(cancellationToken);
    }
}
