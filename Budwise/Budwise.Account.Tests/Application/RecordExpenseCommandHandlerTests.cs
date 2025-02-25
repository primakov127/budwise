using Budwise.Account.Application.Commands;
using Budwise.Account.Application.Handlers;
using Budwise.Account.Domain.Aggregates;
using Budwise.Account.Domain.Entities;
using Budwise.Account.Domain.Errors;
using Budwise.Account.Domain.Events;
using Budwise.Account.Infrastructure.Persistence;
using CSharpFunctionalExtensions;
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
        Assert.False(result.IsSuccess);
        
        // No MoneyWithdrawn event should have been published.
        Assert.False(await harness.Published.Any<MoneyWithdrawn>());
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
        Assert.False(result.IsSuccess);
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
    
        // No MoneyWithdrawn event should have been published.
        Assert.False(await harness.Published.Any<MoneyWithdrawn>());
    }

    // [Fact]
    // public async Task Handle_ShouldReturnFailure_WhenInvalidAmount()
    // {
    //     // Arrange
    //     var accountId = Guid.NewGuid();
    //     var ownerIds = new List<Guid> { Guid.NewGuid() };
    //     // An invalid withdrawal amount (0 in this case).
    //     var command = new RecordExpenseCommand(accountId, 0m, "Test expense");
    //
    //     var newAccount = new AssetAccount(accountId, ownerIds);
    //     newAccount.Deposit(100m, "Initial deposit");
    //     _context.AssetAccounts.Add(newAccount);
    //     await _context.SaveChangesAsync();
    //
    //     // Act
    //     var result = await _handler.Handle(command);
    //
    //     // Assert
    //     Assert.False(result.IsSuccess);
    //     Assert.Equal(ErrorMessage.FromCode(ErrorCode.InvalidAmount), result.Error);
    //
    //     var account = await _context.AssetAccounts
    //         .Include(a => a.Transactions)
    //         .FirstOrDefaultAsync(a => a.AccountId == accountId);
    //
    //     Assert.NotNull(account);
    //     // Balance remains unchanged
    //     Assert.Equal(100m, account.Balance);
    //     // Only the deposit transaction should be present.
    //     Assert.Single(account.Transactions);
    //     // No event published due to invalid amount.
    //     Assert.False(await _harness.Published.Any<MoneyWithdrawn>());
    // }
    //
    // [Fact]
    // public async Task Handle_ShouldWithdrawMultipleExpenses_WhenCalledMultipleTimes()
    // {
    //     // Arrange
    //     var accountId = Guid.NewGuid();
    //     var ownerIds = new List<Guid> { Guid.NewGuid() };
    //     var newAccount = new AssetAccount(accountId, ownerIds);
    //     newAccount.Deposit(200m, "Initial deposit");
    //     _context.AssetAccounts.Add(newAccount);
    //     await _context.SaveChangesAsync();
    //
    //     var command1 = new RecordExpenseCommand(accountId, 50m, "Expense 1");
    //     var command2 = new RecordExpenseCommand(accountId, 30m, "Expense 2");
    //
    //     // Act
    //     var result1 = await _handler.Handle(command1);
    //     var result2 = await _handler.Handle(command2);
    //
    //     // Assert
    //     Assert.True(result1.IsSuccess);
    //     Assert.True(result2.IsSuccess);
    //
    //     var account = await _context.AssetAccounts
    //         .AsNoTracking() // To force a fresh read from the database.
    //         .Include(a => a.Transactions)
    //         .FirstOrDefaultAsync(a => a.AccountId == accountId);
    //
    //     Assert.NotNull(account);
    //     // Balance should be: 200 - 50 - 30 = 120.
    //     Assert.Equal(120m, account.Balance);
    //     // One deposit plus two withdrawals = 3 transactions.
    //     Assert.Equal(3, account.Transactions.Count);
    //
    //     // Two MoneyWithdrawn events should have been published.
    //     var publishedEventsCount = await _harness.Published.SelectAsync<MoneyWithdrawn>().Count();
    //     Assert.Equal(2, publishedEventsCount);
    // }
    //
    // [Fact]
    // public async Task Handle_ShouldMaintainConsistentBalance_WhenMultipleSimultaneousTransactionsAreProcessed()
    // {
    //     // Arrange
    //     // Use an initial deposit that cannot cover all requested simultaneous expenses.
    //     var accountId = Guid.NewGuid();
    //     var ownerIds = new List<Guid> { Guid.NewGuid() };
    //     var initialDeposit = 100m;
    //     var expenseAmount = 30m;
    //     var numConcurrentTransactions = 5; // Maximum total withdrawal if all succeed would be 150, which exceeds the deposit.
    //
    //     // Create account with an initial deposit.
    //     var newAccount = new AssetAccount(accountId, ownerIds);
    //     newAccount.Deposit(initialDeposit, "Initial deposit");
    //     _context.AssetAccounts.Add(newAccount);
    //     await _context.SaveChangesAsync();
    //
    //     // Act
    //     // Simulate concurrent expense transactions. Each task creates its own scope to mimic a separate request.
    //     var tasks = new List<Task<Result>>();
    //     for (int i = 0; i < numConcurrentTransactions; i++)
    //     {
    //         var expenseCommand = new RecordExpenseCommand(accountId, expenseAmount, $"Simultaneous expense {i + 1}");
    //         tasks.Add(Task.Run(async () =>
    //         {
    //             using (var scope = _serviceProvider.CreateScope())
    //             {
    //                 var handler = scope.ServiceProvider.GetRequiredService<RecordExpenseCommandHandler>();
    //                 return await handler.Handle(expenseCommand);
    //             }
    //         }));
    //     }
    //
    //     var results = await Task.WhenAll(tasks);
    //
    //     // we expect 3 credit transactions 30 * 3 = 90 since the balance is 100
    //     var expectedSuccessfulTransactionsCount = 3;
    //
    //     // Assert
    //     var failedTransactions = results.Where(r => r.IsFailure).ToList();
    //     Assert.Equal(results.Length - expectedSuccessfulTransactionsCount, failedTransactions.Count);
    //     Assert.All(failedTransactions, result => Assert.Equal(ErrorMessage.FromCode(ErrorCode.InsufficientFunds), result.Error));
    //     
    //     // Reload the account from the database using a fresh scope.
    //     using (var scope = _serviceProvider.CreateScope())
    //     {
    //         var context = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
    //         var account = await context.AssetAccounts
    //             .Include(a => a.Transactions)
    //             .FirstOrDefaultAsync(a => a.AccountId == accountId);
    //         Assert.NotNull(account);
    //
    //         // The expected final balance is the initial deposit minus the sum of successful withdrawals.
    //         var expectedBalance = initialDeposit - (expectedSuccessfulTransactionsCount * expenseAmount);
    //         Assert.Equal(expectedBalance, account.Balance);
    //
    //         // The account should have one deposit plus one transaction per successful expense.
    //         Assert.Equal(1 + expectedSuccessfulTransactionsCount, account.Transactions.Count);
    //     }
    //
    //     // Optionally, verify that the number of MoneyWithdrawn events published equals the successful transactions.
    //     // Note: Depending on your DI setup, the harness might be different in separate scopes.
    //     // For simplicity, we'll check against the global harness from the test fixture.
    //     var publishedEventsCount = await _harness.Published.SelectAsync<MoneyWithdrawn>().Count();
    //     Assert.Equal(expectedSuccessfulTransactionsCount, publishedEventsCount);
    // }
}