namespace Budwise.Account.Application.Commands;

public class RecordExpenseCommand(Guid accountId, decimal amount)
{
    public Guid AccountId { get; } = accountId;
    public decimal Amount { get; } = amount;
}