using Budwise.Account.Domain.Events;
using MassTransit;

namespace Budwise.Account.Infrastructure.Messaging;

public class AccountEventsPublisher(IPublishEndpoint publishEndpoint)
{
    public async Task PublishMoneyDepositedAsync(MoneyDeposited depositEvent)
    {
        await publishEndpoint.Publish(depositEvent);
    }

    public async Task PublishMoneyWithdrawnAsync(MoneyWithdrawn withdrawEvent)
    {
        await publishEndpoint.Publish(withdrawEvent);
    }
}