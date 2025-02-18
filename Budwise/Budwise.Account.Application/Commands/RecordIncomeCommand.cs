namespace Budwise.Account.Application.Commands;

public class RecordIncomeCommand(Guid accountId, decimal amount)
{
    public Guid AccountId { get; } = accountId;
    public decimal Amount { get; } = amount;
}