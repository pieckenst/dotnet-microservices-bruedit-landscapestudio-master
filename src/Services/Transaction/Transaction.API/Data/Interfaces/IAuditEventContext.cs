using MongoDB.Driver;
using Transaction.API.Entities;

namespace Transaction.API.Data.Interfaces;

public interface IAuditEventContext
{
    IMongoCollection<AuditRecord> AuditEvents { get; }
}
