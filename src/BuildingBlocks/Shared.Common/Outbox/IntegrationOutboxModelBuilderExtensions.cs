using Microsoft.EntityFrameworkCore;

namespace Shared.Common.Outbox;

public static class IntegrationOutboxModelBuilderExtensions
{
    public static ModelBuilder AddIntegrationOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<IntegrationOutboxMessage>(entity =>
        {
            entity.ToTable("integration_outbox");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Id).HasColumnName("id");
            entity.Property(message => message.MessageType)
                .HasColumnName("message_type").HasMaxLength(512).IsRequired();
            entity.Property(message => message.Payload)
                .HasColumnName("payload").IsRequired();
            entity.Property(message => message.CreatedAtUtc)
                .HasColumnName("created_at_utc").IsRequired();
            entity.Property(message => message.NextAttemptAtUtc)
                .HasColumnName("next_attempt_at_utc").IsRequired();
            entity.Property(message => message.LeaseExpiresAtUtc)
                .HasColumnName("lease_expires_at_utc");
            entity.Property(message => message.PublishedAtUtc)
                .HasColumnName("published_at_utc");
            entity.Property(message => message.DeadLetteredAtUtc)
                .HasColumnName("dead_lettered_at_utc");
            entity.Property(message => message.AttemptCount)
                .HasColumnName("attempt_count").IsRequired();
            entity.Property(message => message.Version)
                .HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(message => message.LastError)
                .HasColumnName("last_error").HasMaxLength(2000);
            entity.HasIndex(message => new
            {
                message.PublishedAtUtc,
                message.DeadLetteredAtUtc,
                message.NextAttemptAtUtc,
                message.LeaseExpiresAtUtc
            }).HasDatabaseName("IX_integration_outbox_dispatch");
        });

        return modelBuilder;
    }
}
