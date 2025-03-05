using Budwise.Analytics.Domain.Events;

namespace Budwise.Analytics.Domain.Aggregates;

public class MonthlyExpense
{
    public Guid Id { get; set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal TotalExpenses { get; private set; }
    // Now named TagExpenses – each tag maps to its aggregated expense amount.
    public Dictionary<string, decimal> TagExpenses { get; private set; }

    public MonthlyExpense(Guid id, int year, int month)
    {
        Id = id;
        Year = year;
        Month = month;
        TotalExpenses = 0;
        TagExpenses = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    }

    // Behavior: add an expense with multiple tags.
    public void AddExpense(decimal amount, IEnumerable<string> tags, DateTime expenseDate)
    {
        if (expenseDate.Year != Year || expenseDate.Month != Month)
            throw new ArgumentException("Expense date does not match the aggregate period.");

        // Create an ExpenseAdded event with the provided tags.
        var expenseEvent = new ExpenseAdded(Guid.NewGuid(), amount, tags, expenseDate);
        // ApplyChange(expenseEvent);
    }
}