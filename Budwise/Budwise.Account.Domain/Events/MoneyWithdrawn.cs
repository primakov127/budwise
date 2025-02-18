namespace Budwise.Account.Domain.Events;

public class MoneyWithdrawn(Guid accountId, decimal amount)
{
    public Guid AccountId { get; } = accountId;
    public decimal Amount { get; } = amount;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}