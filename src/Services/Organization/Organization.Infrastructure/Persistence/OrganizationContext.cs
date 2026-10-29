using Microsoft.EntityFrameworkCore;
using Organization.Domain.Entities;
using Shared.Common.Common;

namespace Organization.Infrastructure.Persistence;

public sealed class OrganizationContext(DbContextOptions<OrganizationContext> options) : DbContext(options)
{
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Brigade> Brigades => Set<Brigade>();
    public DbSet<BrigadeMember> BrigadeMembers => Set<BrigadeMember>();
    public DbSet<WorkAssignment> WorkAssignments => Set<WorkAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureEntity<Position>(modelBuilder);
        ConfigureEntity<Employee>(modelBuilder);
        ConfigureEntity<Brigade>(modelBuilder);
        ConfigureEntity<BrigadeMember>(modelBuilder);
        ConfigureEntity<WorkAssignment>(modelBuilder);

        modelBuilder.Entity<Position>(entity =>
        {
            entity.ToTable("positions");
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.Rank });
        });
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("employees");
            entity.Property(x => x.EmployeeNumber).HasMaxLength(32).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Ignore(x => x.FullName);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(x => x.EmployeeNumber).IsUnique();
            entity.HasIndex(x => x.IdentityUserId);
            entity.HasOne(x => x.Position).WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Brigade>(entity =>
        {
            entity.ToTable("brigades");
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Specialization).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasMany(x => x.Members).WithOne(x => x.Brigade).HasForeignKey(x => x.BrigadeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.ForepersonEmployeeId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<BrigadeMember>(entity =>
        {
            entity.ToTable("brigade_members");
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(x => new { x.BrigadeId, x.EmployeeId, x.LeftDate });
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WorkAssignment>(entity =>
        {
            entity.ToTable("work_assignments");
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Notes).HasMaxLength(2000).IsRequired();
            entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Brigade>().WithMany().HasForeignKey(x => x.BrigadeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ProjectId, x.Status });
            entity.HasIndex(x => x.ProjectTaskId);
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
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }
}
