namespace Budwise.Account.Domain.Entities;

public class Transaction(
    Guid transactionId,
    Guid accountId,
    decimal amount,
    DateTime date,
    TransactionType type,
    string? note)
{
    public Guid TransactionId { get; } = transactionId;
    public Guid AccountId { get; } = accountId;
    public decimal Amount { get; } = amount;
    public DateTime Date { get; } = date;
    public TransactionType Type { get; } = type;
    public string? Note { get; } = note;
}