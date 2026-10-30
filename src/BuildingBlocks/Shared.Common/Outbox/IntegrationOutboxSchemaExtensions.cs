using Microsoft.EntityFrameworkCore;

namespace Shared.Common.Outbox;

/// <summary>
/// Idempotently provisions the outbox table for services that still use EnsureCreated
/// instead of a full migration history. This is deliberately limited to the table owned
/// by Shared.Common; it does not attempt to upgrade any service's business schema.
/// </summary>
public static class IntegrationOutboxSchemaExtensions
{
    public static Task EnsureIntegrationOutboxSchemaAsync(
        this DatabaseFacade database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        var provider = database.ProviderName ?? string.Empty;

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return database.ExecuteSqlRawAsync(PostgresSql, cancellationToken);
        }

        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return database.ExecuteSqlRawAsync(SqliteSql, cancellationToken);
        }

        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return database.ExecuteSqlRawAsync(SqlServerSql, cancellationToken);
        }

        throw new NotSupportedException(
            $"Integration outbox schema provisioning is not supported for EF provider '{provider}'.");
    }

    private const string PostgresSql = """
        CREATE TABLE IF NOT EXISTS integration_outbox (
            id uuid NOT NULL CONSTRAINT "PK_integration_outbox" PRIMARY KEY,
            message_type character varying(512) NOT NULL,
            payload text NOT NULL,
            created_at_utc timestamp with time zone NOT NULL,
            next_attempt_at_utc timestamp with time zone NOT NULL,
            lease_expires_at_utc timestamp with time zone NULL,
            published_at_utc timestamp with time zone NULL,
            dead_lettered_at_utc timestamp with time zone NULL,
            attempt_count integer NOT NULL,
            version bigint NOT NULL,
            last_error character varying(2000) NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_integration_outbox_dispatch"
            ON integration_outbox (published_at_utc, dead_lettered_at_utc, next_attempt_at_utc, lease_expires_at_utc);
        """;

    private const string SqliteSql = """
        CREATE TABLE IF NOT EXISTS "integration_outbox" (
            "id" TEXT NOT NULL CONSTRAINT "PK_integration_outbox" PRIMARY KEY,
            "message_type" TEXT NOT NULL,
            "payload" TEXT NOT NULL,
            "created_at_utc" TEXT NOT NULL,
            "next_attempt_at_utc" TEXT NOT NULL,
            "lease_expires_at_utc" TEXT NULL,
            "published_at_utc" TEXT NULL,
            "dead_lettered_at_utc" TEXT NULL,
            "attempt_count" INTEGER NOT NULL,
            "version" INTEGER NOT NULL,
            "last_error" TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_integration_outbox_dispatch"
            ON "integration_outbox" ("published_at_utc", "dead_lettered_at_utc", "next_attempt_at_utc", "lease_expires_at_utc");
        """;

    private const string SqlServerSql = """
        IF OBJECT_ID(N'dbo.integration_outbox', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[integration_outbox] (
                [id] uniqueidentifier NOT NULL CONSTRAINT [PK_integration_outbox] PRIMARY KEY,
                [message_type] nvarchar(512) NOT NULL,
                [payload] nvarchar(max) NOT NULL,
                [created_at_utc] datetime2 NOT NULL,
                [next_attempt_at_utc] datetime2 NOT NULL,
                [lease_expires_at_utc] datetime2 NULL,
                [published_at_utc] datetime2 NULL,
                [dead_lettered_at_utc] datetime2 NULL,
                [attempt_count] int NOT NULL,
                [version] bigint NOT NULL,
                [last_error] nvarchar(2000) NULL
            );
        END;
        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_integration_outbox_dispatch'
              AND object_id = OBJECT_ID(N'dbo.integration_outbox')
        )
        BEGIN
            CREATE INDEX [IX_integration_outbox_dispatch]
                ON [dbo].[integration_outbox]
                ([published_at_utc], [dead_lettered_at_utc], [next_attempt_at_utc], [lease_expires_at_utc]);
        END;
        """;
}
