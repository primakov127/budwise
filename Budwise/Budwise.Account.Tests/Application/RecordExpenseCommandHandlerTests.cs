using Budwise.Account.Application.Commands;
using Budwise.Account.Application.Handlers;
using Budwise.Account.Domain.Aggregates;
using Budwise.Account.Domain.Entities;
using Budwise.Account.Domain.Errors;
using Budwise.Account.Domain.Events;
using Budwise.Account.Infrastructure.Persistence;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Budwise.Account.Tests.Application;

public class RecordExpenseCommandHandlerTests(TestFixture fixture) : IClassFixture<TestFixture>
{
    [Fact]
    public async Task Handle_ShouldWithdrawAmount_WhenAccountExists()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AccountDbContext>>();
        var handler = scope.ServiceProvider.GetRequiredService<RecordExpenseCommandHandler>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();

        const decimal initialDeposit = 100m;
        const decimal expenseAmount = 50m;
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        var command = new RecordExpenseCommand(accountId, expenseAmount, "Test expense");

        await using (var writeContext = await dbContextFactory.CreateDbContextAsync())
        {
            var newAccount = new AssetAccount(accountId, ownerIds);
            newAccount.Deposit(initialDeposit, "Initial deposit");

            writeContext.AssetAccounts.Add(newAccount);

            await writeContext.SaveChangesAsync();
        }

        // Act
        var result = await handler.Handle(command);

        // Assert
        Assert.True(result.IsSuccess);

        await using (var readContext = await dbContextFactory.CreateDbContextAsync())
        {
            var account = await readContext.AssetAccounts
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            Assert.NotNull(account);
            Assert.Equal(initialDeposit - expenseAmount, account.Balance);
            Assert.Equal(2, account.Transactions.Count);

            var creditTransaction = account.Transactions.OrderBy(t => t.Date).Last();
            Assert.Equal(TransactionType.Credit, creditTransaction.Type);
            Assert.Equal(expenseAmount, creditTransaction.Amount);
        }

        var publishedEvent = harness.Published
            .Select<MoneyWithdrawn>()
            .FirstOrDefault(e => e.Context.Message.AccountId == accountId)?
            .Context.Message;

        Assert.NotNull(publishedEvent);
        Assert.Equal(expenseAmount, publishedEvent.Amount);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenAccountDoesNotExist()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RecordExpenseCommandHandler>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();

        const decimal expenseAmount = 50m;
        var accountId = Guid.NewGuid();
        var command = new RecordExpenseCommand(accountId, expenseAmount, "Test expense");

        // Act
        var result = await handler.Handle(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.AccountNotFound), result.Error);

        var publishedEvent = harness.Published
            .Select<MoneyWithdrawn>()
            .FirstOrDefault(e => e.Context.Message.AccountId == accountId)?
            .Context.Message;

        Assert.Null(publishedEvent);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenInsufficientFunds()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AccountDbContext>>();
        var handler = scope.ServiceProvider.GetRequiredService<RecordExpenseCommandHandler>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();

        const decimal initialDeposit = 100m;
        const decimal expenseAmount = 150m;
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        var command = new RecordExpenseCommand(accountId, expenseAmount, "Test expense");

        await using (var writeContext = await dbContextFactory.CreateDbContextAsync())
        {
            var newAccount = new AssetAccount(accountId, ownerIds);
            newAccount.Deposit(initialDeposit, "Initial deposit");

            writeContext.AssetAccounts.Add(newAccount);

            await writeContext.SaveChangesAsync();
        }

        // Act
        var result = await handler.Handle(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.InsufficientFunds), result.Error);

        await using (var readContext = await dbContextFactory.CreateDbContextAsync())
        {
            var account = await readContext.AssetAccounts
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            Assert.NotNull(account);
            // The balance remains unchanged due to insufficient funds.
            Assert.Equal(initialDeposit, account.Balance);
            // Only the initial deposit transaction should exist.
            Assert.Single(account.Transactions);
        }

        var publishedEvent = harness.Published
            .Select<MoneyWithdrawn>()
            .FirstOrDefault(e => e.Context.Message.AccountId == accountId)?
            .Context.Message;

        Assert.Null(publishedEvent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task Handle_ShouldReturnFailure_WhenInvalidAmount(decimal invalidExpenseAmount)
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AccountDbContext>>();
        var handler = scope.ServiceProvider.GetRequiredService<RecordExpenseCommandHandler>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();

        const decimal initialDeposit = 100m;
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        var command = new RecordExpenseCommand(accountId, invalidExpenseAmount, "Test expense");

        await using (var writeContext = await dbContextFactory.CreateDbContextAsync())
        {
            var newAccount = new AssetAccount(accountId, ownerIds);
            newAccount.Deposit(initialDeposit, "Initial deposit");

            writeContext.AssetAccounts.Add(newAccount);

            await writeContext.SaveChangesAsync();
        }

        // Act
        var result = await handler.Handle(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.InvalidAmount), result.Error);

        await using (var readContext = await dbContextFactory.CreateDbContextAsync())
        {
            var account = await readContext.AssetAccounts
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            Assert.NotNull(account);
            // The balance remains unchanged since the expense amount is invalid.
            Assert.Equal(initialDeposit, account.Balance);
            // Only the initial deposit transaction should exist.
            Assert.Single(account.Transactions);
        }

        var publishedEvent = harness.Published
            .Select<MoneyWithdrawn>()
            .FirstOrDefault(e => e.Context.Message.AccountId == accountId)?
            .Context.Message;

        Assert.Null(publishedEvent);
    }

    [Fact]
    public async Task Handle_ShouldWithdrawMultipleExpenses_WhenCalledMultipleTimes()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AccountDbContext>>();
        var handler = scope.ServiceProvider.GetRequiredService<RecordExpenseCommandHandler>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();

        const decimal initialDeposit = 200m;
        decimal[] expenseAmounts = [50m, 30m];
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };

        await using (var writeContext = await dbContextFactory.CreateDbContextAsync())
        {
            var newAccount = new AssetAccount(accountId, ownerIds);
            newAccount.Deposit(initialDeposit, "Initial deposit");

            writeContext.AssetAccounts.Add(newAccount);

            await writeContext.SaveChangesAsync();
        }

        var commands = expenseAmounts.Select((amount, index) =>
            new RecordExpenseCommand(accountId, amount, $"Expense {index + 1}"));

        // Act
        foreach (var command in commands)
        {
            var result = await handler.Handle(command);

            Assert.True(result.IsSuccess);
        }

        // Assert
        await using (var readContext = await dbContextFactory.CreateDbContextAsync())
        {
            var account = await readContext.AssetAccounts
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            Assert.NotNull(account);
            // The expected balance after both withdrawals is the initial deposit minus the sum of the expenses.
            Assert.Equal(initialDeposit - expenseAmounts.Sum(), account.Balance);
        }

        var publishedEvents = harness.Published
            .Select<MoneyWithdrawn>()
            .Where(e => e.Context.Message.AccountId == accountId)
            .Select(e => e.Context.Message)
            .ToArray();

        Assert.Equal(2, publishedEvents.Length);
        Assert.Contains(publishedEvents, msg => msg.Amount == 50m);
        Assert.Contains(publishedEvents, msg => msg.Amount == 30m);
    }

    [Fact]
    public async Task Handle_ShouldMaintainConsistentBalance_WhenMultipleSimultaneousTransactionsAreProcessed()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AccountDbContext>>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();

        const decimal initialDeposit = 100m;
        const decimal expenseAmount = 30m;
        const int numConcurrentTransactions = 5; // If all succeed, total withdrawal would be 150, exceeding the deposit.
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };

        await using (var writeContext = await dbContextFactory.CreateDbContextAsync())
        {
            var newAccount = new AssetAccount(accountId, ownerIds);
            newAccount.Deposit(initialDeposit, "Initial deposit");
            
            writeContext.AssetAccounts.Add(newAccount);
            
            await writeContext.SaveChangesAsync();
        }

        // Act
        // Simulate concurrent expense transactions by creating a new scope per task.
        var tasks = Enumerable.Range(0, numConcurrentTransactions)
            .Select(i => Task.Run(async () =>
            {
                using var innerScope = fixture.ServiceProvider.CreateScope();
                var handler = innerScope.ServiceProvider.GetRequiredService<RecordExpenseCommandHandler>();

                return await handler.Handle(
                    new RecordExpenseCommand(accountId, expenseAmount, $"Simultaneous expense {i + 1}"));
            }));

        var results = await Task.WhenAll(tasks);

        // We expect only 3 expenses to succeed (3 * 30 = 90) since the deposit is 100.
        const int expectedSuccessfulTransactionsCount = 3;

        // Assert
        var successfulTransactions = results.Where(r => r.IsSuccess).ToArray();
        Assert.Equal(expectedSuccessfulTransactionsCount, successfulTransactions.Length);
        
        var failedTransactions = results.Where(r => r.IsFailure).ToArray();
        Assert.Equal(numConcurrentTransactions - expectedSuccessfulTransactionsCount, failedTransactions.Length);
        Assert.All(failedTransactions, result =>
            Assert.Equal(ErrorMessage.FromCode(ErrorCode.InsufficientFunds), result.Error));

        await using (var readContext = await dbContextFactory.CreateDbContextAsync())
        {
            var account = await readContext.AssetAccounts
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);
            
            Assert.NotNull(account);

            const decimal expectedBalance = initialDeposit - (expectedSuccessfulTransactionsCount * expenseAmount);
            Assert.Equal(expectedBalance, account.Balance);

            // There is one deposit plus one transaction for each successful expense.
            Assert.Equal(1 + expectedSuccessfulTransactionsCount, account.Transactions.Count);
        }

        var publishedEvents = harness.Published
            .Select<MoneyWithdrawn>()
            .Where(e => e.Context.Message.AccountId == accountId)
            .Select(e => e.Context.Message)
            .ToArray();
        
        Assert.Equal(expectedSuccessfulTransactionsCount, publishedEvents.Length);
        Assert.All(publishedEvents, msg => Assert.Equal(expenseAmount, msg.Amount));
    }
}