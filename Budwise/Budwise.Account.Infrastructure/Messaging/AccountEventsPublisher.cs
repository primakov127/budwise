using Budwise.Account.Domain.Events;
using MassTransit;

namespace Budwise.Account.Infrastructure.Messaging;

public class AccountEventsPublisher(IPublishEndpoint publishEndpoint)
{
    public async Task PublishMoneyDeposited(MoneyDeposited depositEvent)
    {
        await publishEndpoint.Publish(depositEvent);
    }

    public async Task PublishMoneyWithdrawn(MoneyWithdrawn withdrawEvent)
    {
        await publishEndpoint.Publish(withdrawEvent);
    }
}