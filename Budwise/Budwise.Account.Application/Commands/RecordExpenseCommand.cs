namespace Budwise.Account.Application.Commands;

public class RecordExpenseCommand(Guid accountId, decimal amount, string? note)
{
    public Guid AccountId { get; private set; } = accountId;
    public decimal Amount { get; private set; } = amount;
    public string? Note { get; private set; } = note;
}