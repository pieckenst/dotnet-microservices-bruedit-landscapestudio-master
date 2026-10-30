using Account.Domain.Common;
using Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;

using Shared.Common.Outbox;
namespace Account.Infrastructure.Persistence;

public sealed class AccountContext(DbContextOptions<AccountContext> options) : DbContext(options)
{
    public DbSet<AccountProfile> AccountProfiles => Set<AccountProfile>();
    public DbSet<AccountPreference> AccountPreferences => Set<AccountPreference>();
    public DbSet<AccountAddress> AccountAddresses => Set<AccountAddress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddIntegrationOutbox();

        modelBuilder.Entity<AccountProfile>(entity =>
        {
            entity.ToTable("AccountProfiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
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
            entity.Property(x => x.Theme).HasMaxLength(16).IsRequired().HasDefaultValue("system");
            entity.Property(x => x.Language).HasMaxLength(8).IsRequired().HasDefaultValue("en");
            entity.Property(x => x.TimeZone).HasMaxLength(80).IsRequired().HasDefaultValue("UTC");
            entity.Property(x => x.EmailNotificationsEnabled).HasDefaultValue(true);
            entity.Property(x => x.SmsNotificationsEnabled).HasDefaultValue(false);
            entity.Property(x => x.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne<AccountProfile>().WithOne()
                .HasForeignKey<AccountPreference>(x => x.Id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountAddress>(entity =>
        {
            entity.ToTable("AccountAddresses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Label).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Line1).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Line2).HasMaxLength(160);
            entity.Property(x => x.City).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Region).HasMaxLength(100);
            entity.Property(x => x.PostalCode).HasMaxLength(24);
            entity.Property(x => x.Country).HasMaxLength(2).IsRequired();
            entity.Property(x => x.IsDefault).HasDefaultValue(false);
            entity.Property(x => x.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => new { x.UserId, x.IsDefault });
            entity.HasOne<AccountProfile>().WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// The SQLite fallback can outlive upgrades. Older versions created an Accounts banking
    /// table, which causes EnsureCreated to return without creating profile tables. Rebuild
    /// that obsolete-only database; preserve a complete profile schema on subsequent starts.
    /// </summary>
    public async Task EnsureSqliteSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
            throw new InvalidOperationException("EnsureSqliteSchemaAsync can only be used with SQLite.");

        var profileTableCount = 0;
        if (await Database.CanConnectAsync(cancellationToken))
        {
            await Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = Database.GetDbConnection().CreateCommand();
                command.CommandText = """
                    SELECT COUNT(*)
                    FROM sqlite_master
                    WHERE type = 'table'
                      AND name IN ('AccountProfiles', 'AccountPreferences', 'AccountAddresses');
                    """;
                profileTableCount = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            }
            finally
            {
                await Database.CloseConnectionAsync();
            }

            if (profileTableCount == 0)
                await Database.EnsureDeletedAsync(cancellationToken);
            else if (profileTableCount != 3)
                throw new InvalidOperationException(
                    "The SQLite Account database contains an incomplete profile schema. Back up the file and restore it from a valid Account database backup.");
        }

        await Database.EnsureCreatedAsync(cancellationToken);
        await Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS \"Accounts\";", cancellationToken);
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
