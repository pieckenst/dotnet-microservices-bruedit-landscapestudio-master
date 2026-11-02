using System.Text.Json;
using EventBus.Messages.Events;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Outbox;

namespace Shared.ServiceKit.Outbox;

public sealed class EfIntegrationOutbox<TContext>(TContext context) : IIntegrationOutbox
    where TContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public void Enqueue(IntegrationBaseEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var type = message.GetType();
        if (type.IsAbstract || !typeof(IntegrationBaseEvent).IsAssignableFrom(type) ||
            type.Assembly != typeof(IntegrationBaseEvent).Assembly)
            throw new ArgumentException("Only registered EventBus.Messages integration events can be queued.", nameof(message));

        var payload = JsonSerializer.Serialize(message, type, JsonOptions);
        context.Set<IntegrationOutboxMessage>().Add(
            new IntegrationOutboxMessage(type.FullName!, payload, DateTime.UtcNow));
    }
}
