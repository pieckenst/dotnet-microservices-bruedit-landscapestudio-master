using MongoDB.Driver;
using Transaction.API.Data.Interfaces;
using Transaction.API.Entities;

namespace Transaction.API.Data;

public sealed class AuditEventContext : IAuditEventContext
{
    public AuditEventContext(IConfiguration configuration)
    {
        var connectionString = configuration["DatabaseSettings:ConnectionString"];
        var databaseName = configuration["DatabaseSettings:DatabaseName"];
        var collectionName = configuration["DatabaseSettings:CollectionName"];
        if (string.IsNullOrWhiteSpace(connectionString) ||
            string.IsNullOrWhiteSpace(databaseName) ||
            string.IsNullOrWhiteSpace(collectionName))
            throw new InvalidOperationException("Audit database settings are incomplete.");

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        AuditEvents = database.GetCollection<AuditRecord>(collectionName);
        AuditEvents.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<AuditRecord>(Builders<AuditRecord>.IndexKeys.Descending(x => x.OccurredAtUtc).Descending(x => x.Id)),
            new CreateIndexModel<AuditRecord>(Builders<AuditRecord>.IndexKeys.Ascending(x => x.SourceService).Descending(x => x.OccurredAtUtc)),
            new CreateIndexModel<AuditRecord>(Builders<AuditRecord>.IndexKeys.Ascending(x => x.EntityType).Ascending(x => x.EntityId)),
            new CreateIndexModel<AuditRecord>(Builders<AuditRecord>.IndexKeys.Ascending(x => x.ActorId).Descending(x => x.OccurredAtUtc))
        });
    }

    public IMongoCollection<AuditRecord> AuditEvents { get; }
}
