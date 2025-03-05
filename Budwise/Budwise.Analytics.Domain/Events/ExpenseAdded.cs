namespace Budwise.Analytics.Domain.Events;

public class ExpenseAdded
{
    public Guid ExpenseId { get; }
    public decimal Amount { get; }
    public IEnumerable<string> Tags { get; }  // Multiple tags
    public DateTime OccurredOn { get; protected set; }

    public ExpenseAdded(Guid expenseId, decimal amount, IEnumerable<string> tags, DateTime occurredOn)
    {
        OccurredOn = occurredOn;
        ExpenseId = expenseId;
        Amount = amount;
        Tags = tags;
    }
}