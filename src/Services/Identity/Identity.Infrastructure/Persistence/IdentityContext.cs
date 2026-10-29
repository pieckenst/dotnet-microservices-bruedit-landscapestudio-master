using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Common;

namespace Identity.Infrastructure.Persistence;

public sealed class IdentityContext(DbContextOptions<IdentityContext> options) : DbContext(options)
{
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureEntity<UserCredential>(modelBuilder);
        ConfigureEntity<Role>(modelBuilder);
        ConfigureEntity<Permission>(modelBuilder);
        ConfigureEntity<RolePermission>(modelBuilder);

        modelBuilder.Entity<UserCredential>(entity =>
        {
            entity.ToTable("user_credentials");
            entity.Property(x => x.UserName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.NormalizedUserName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.FailedAccessCount).IsRequired();
            entity.HasIndex(x => x.NormalizedUserName).IsUnique();
            entity.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.Property(x => x.Name).HasMaxLength(64).IsRequired();
            entity.Property(x => x.NormalizedName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => x.NormalizedName).IsUnique();
        });
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.NormalizedName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => x.NormalizedName).IsUnique();
        });
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
            entity.HasOne(x => x.Role).WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Permission).WithMany()
                .HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureEntity<TEntity>(ModelBuilder modelBuilder)
        where TEntity : EntityBase
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CreatedDate).IsRequired();
            entity.Property(item => item.LastModifiedDate);
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
    public override int SaveChanges()
    {
        StampAuditFields();
        return base.SaveChanges();
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }
}
