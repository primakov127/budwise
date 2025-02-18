namespace Budwise.Account.Infrastructure.EventStore;

public class AccountEventStore
{
    // In-memory event store for demonstration purposes.
    private readonly List<object> _events = [];

    public Task SaveEventAsync(object domainEvent)
    {
        _events.Add(domainEvent);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<object>> GetEventsAsync(string aggregateId)
    {
        // For simplicity, we return all events.
        return Task.FromResult<IEnumerable<object>>(_events);
    }
}