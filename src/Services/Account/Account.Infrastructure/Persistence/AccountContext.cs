using Account.Domain.Common;
using Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Account.Infrastructure.Persistence;

public sealed class AccountContext(DbContextOptions<AccountContext> options) : DbContext(options)
{
    public DbSet<Domain.Entities.Account> Accounts => Set<Domain.Entities.Account>();
    public DbSet<AccountProfile> AccountProfiles => Set<AccountProfile>();
    public DbSet<AccountPreference> AccountPreferences => Set<AccountPreference>();
    public DbSet<AccountAddress> AccountAddresses => Set<AccountAddress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Domain.Entities.Account>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Balance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<AccountProfile>(entity =>
        {
            entity.ToTable("AccountProfiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(80);
            entity.Property(x => x.LastName).HasMaxLength(80);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.PhoneNumber).HasMaxLength(40);
            entity.Property(x => x.CompanyName).HasMaxLength(160);
            entity.HasIndex(x => x.Email);
        });

        modelBuilder.Entity<AccountPreference>(entity =>
        {
            entity.ToTable("AccountPreferences");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Theme).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Language).HasMaxLength(8).IsRequired();
            entity.Property(x => x.TimeZone).HasMaxLength(80).IsRequired();
            entity.HasOne<AccountProfile>().WithOne()
                .HasForeignKey<AccountPreference>(x => x.Id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountAddress>(entity =>
        {
            entity.ToTable("AccountAddresses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Label).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Line1).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Line2).HasMaxLength(160);
            entity.Property(x => x.City).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Region).HasMaxLength(100);
            entity.Property(x => x.PostalCode).HasMaxLength(24);
            entity.Property(x => x.Country).HasMaxLength(2).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.IsDefault });
            entity.HasOne<AccountProfile>().WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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
