using Budwise.Account.Application.Commands;
using Budwise.Account.Application.Handlers;
using Budwise.Account.Domain.Aggregates;
using Budwise.Account.Domain.Entities;
using Budwise.Account.Domain.Errors;
using Budwise.Account.Domain.Events;
using Budwise.Account.Infrastructure.Messaging;
using Budwise.Account.Infrastructure.Persistence;
using CSharpFunctionalExtensions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Budwise.Account.Tests.Application;

public class RecordExpenseCommandHandlerTests : IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly AccountDbContext _context;
    private readonly RecordExpenseCommandHandler _handler;
    private readonly ITestHarness _harness;

    public RecordExpenseCommandHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AccountDbContext>(options => options.UseInMemoryDatabase("TestDatabase"));
        services.AddMassTransitTestHarness();
        services.AddScoped<AccountEventsPublisher>();
        services.AddScoped<RecordExpenseCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        _handler = _scope.ServiceProvider.GetRequiredService<RecordExpenseCommandHandler>();
        _harness = _scope.ServiceProvider.GetRequiredService<ITestHarness>();
    }

    public async Task InitializeAsync()
    {
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        _scope.Dispose();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ShouldWithdrawAmount_WhenAccountExists()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        var command = new RecordExpenseCommand(accountId, 50m, "Test expense");

        var newAccount = new AssetAccount(accountId, ownerIds);
        newAccount.Deposit(100m, "Initial deposit");
        _context.AssetAccounts.Add(newAccount);
        await _context.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(command);

        // Assert
        Assert.True(result.IsSuccess);

        var account = await _context.AssetAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.AccountId == accountId);

        Assert.NotNull(account);
        Assert.Equal(50m, account.Balance);
        Assert.Equal(2, account.Transactions.Count);
        Assert.Equal(TransactionType.Credit, account.Transactions[1].Type);

        Assert.True(await _harness.Published.Any<MoneyWithdrawn>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenAccountDoesNotExist()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var command = new RecordExpenseCommand(accountId, 50m, "Test expense");

        // Act
        var result = await _handler.Handle(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.AccountNotFound), result.Error);
        // No event should be published since the account does not exist.
        Assert.False(await _harness.Published.Any<MoneyWithdrawn>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenInsufficientFunds()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        // Attempt to withdraw more than available funds.
        var command = new RecordExpenseCommand(accountId, 150m, "Test expense");

        var newAccount = new AssetAccount(accountId, ownerIds);
        newAccount.Deposit(100m, "Initial deposit");
        _context.AssetAccounts.Add(newAccount);
        await _context.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.InsufficientFunds), result.Error);

        var account = await _context.AssetAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.AccountId == accountId);

        Assert.NotNull(account);
        // Balance remains unchanged
        Assert.Equal(100m, account.Balance);
        // Only the deposit transaction should exist.
        Assert.Single(account.Transactions);
        // No event published due to failure.
        Assert.False(await _harness.Published.Any<MoneyWithdrawn>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenInvalidAmount()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        // An invalid withdrawal amount (0 in this case).
        var command = new RecordExpenseCommand(accountId, 0m, "Test expense");

        var newAccount = new AssetAccount(accountId, ownerIds);
        newAccount.Deposit(100m, "Initial deposit");
        _context.AssetAccounts.Add(newAccount);
        await _context.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.InvalidAmount), result.Error);

        var account = await _context.AssetAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.AccountId == accountId);

        Assert.NotNull(account);
        // Balance remains unchanged
        Assert.Equal(100m, account.Balance);
        // Only the deposit transaction should be present.
        Assert.Single(account.Transactions);
        // No event published due to invalid amount.
        Assert.False(await _harness.Published.Any<MoneyWithdrawn>());
    }

    [Fact]
    public async Task Handle_ShouldWithdrawMultipleExpenses_WhenCalledMultipleTimes()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        var newAccount = new AssetAccount(accountId, ownerIds);
        newAccount.Deposit(200m, "Initial deposit");
        _context.AssetAccounts.Add(newAccount);
        await _context.SaveChangesAsync();

        var command1 = new RecordExpenseCommand(accountId, 50m, "Expense 1");
        var command2 = new RecordExpenseCommand(accountId, 30m, "Expense 2");

        // Act
        var result1 = await _handler.Handle(command1);
        var result2 = await _handler.Handle(command2);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);

        var account = await _context.AssetAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.AccountId == accountId);

        Assert.NotNull(account);
        // Balance should be: 200 - 50 - 30 = 120.
        Assert.Equal(120m, account.Balance);
        // One deposit plus two withdrawals = 3 transactions.
        Assert.Equal(3, account.Transactions.Count);

        // Two MoneyWithdrawn events should have been published.
        var publishedEventsCount = await _harness.Published.SelectAsync<MoneyWithdrawn>().Count();
        Assert.Equal(2, publishedEventsCount);
    }

    [Fact]
    public async Task Handle_ShouldMaintainConsistentBalance_WhenMultipleSimultaneousTransactionsAreProcessed()
    {
        // Arrange
        // Use an initial deposit that cannot cover all requested simultaneous expenses.
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        var initialDeposit = 100m;
        var expenseAmount = 30m;
        var numConcurrentTransactions =
            5; // Maximum total withdrawal if all succeed would be 150, which exceeds the deposit.

        // Create account with an initial deposit.
        var newAccount = new AssetAccount(accountId, ownerIds);
        newAccount.Deposit(initialDeposit, "Initial deposit");
        _context.AssetAccounts.Add(newAccount);
        await _context.SaveChangesAsync();

        // Act
        // Simulate concurrent expense transactions. Each task creates its own scope to mimic a separate request.
        var tasks = new List<Task<Result>>();
        for (int i = 0; i < numConcurrentTransactions; i++)
        {
            var expenseCommand = new RecordExpenseCommand(accountId, expenseAmount, $"Simultaneous expense {i + 1}");
            tasks.Add(Task.Run(async () =>
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var handler = scope.ServiceProvider.GetRequiredService<RecordExpenseCommandHandler>();
                    return await handler.Handle(expenseCommand);
                }
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Count how many transactions were successful.
        int successfulTransactions = results.Count(r => r.IsSuccess);

        // Assert
        // Reload the account from the database using a fresh scope.
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
            var account = await context.AssetAccounts
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);
            Assert.NotNull(account);

            // The expected final balance is the initial deposit minus the sum of successful withdrawals.
            var expectedBalance = initialDeposit - (successfulTransactions * expenseAmount);
            Assert.Equal(expectedBalance, account.Balance);

            // The account should have one deposit plus one transaction per successful expense.
            Assert.Equal(1 + successfulTransactions, account.Transactions.Count);
        }

        // Optionally, verify that the number of MoneyWithdrawn events published equals the successful transactions.
        // Note: Depending on your DI setup, the harness might be different in separate scopes.
        // For simplicity, we'll check against the global harness from the test fixture.
        var publishedEventsCount = await _harness.Published.SelectAsync<MoneyWithdrawn>().Count();
        Assert.Equal(successfulTransactions, publishedEventsCount);
    }
}