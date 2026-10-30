using EventBus.Messages.Events;

namespace Shared.ServiceKit.Outbox;

/// <summary>
/// Adds an integration event to the caller's current relational unit of work.
/// The HTTP transaction middleware persists it with the business data.
/// </summary>
public interface IIntegrationOutbox
{
    void Enqueue(IntegrationBaseEvent message);
}
