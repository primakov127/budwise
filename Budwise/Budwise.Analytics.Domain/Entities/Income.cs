namespace Budwise.Analytics.Domain.Entities;

public class Income
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public List<string> Tags { get; set; }
    public string Note { get; set; }
    public DateTime Date { get; set; }
}