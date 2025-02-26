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

public class RecordIncomeCommandHandlerTests(TestFixture fixture) : IClassFixture<TestFixture>
{
    [Fact]
    public async Task Handle_ShouldDepositAmount_WhenAccountExists()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AccountDbContext>>();
        var handler = scope.ServiceProvider.GetRequiredService<RecordIncomeCommandHandler>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();

        const decimal incomeAmount = 50m;
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        var command = new RecordIncomeCommand(accountId, incomeAmount, "Test income");

        await using (var writeContext = await dbContextFactory.CreateDbContextAsync())
        {
            var newAccount = new AssetAccount(accountId, ownerIds);

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
            Assert.Equal(incomeAmount, account.Balance);
            Assert.Single(account.Transactions);

            var transaction = account.Transactions.FirstOrDefault();
            Assert.NotNull(transaction);
            Assert.Equal(TransactionType.Debit, transaction.Type);
            Assert.Equal(incomeAmount, transaction.Amount);
        }

        var publishedEvent = harness.Published
            .Select<MoneyDeposited>()
            .FirstOrDefault(e => e.Context.Message.AccountId == accountId)?
            .Context.Message;

        Assert.NotNull(publishedEvent);
        Assert.Equal(incomeAmount, publishedEvent.Amount);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenAccountDoesNotExist()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RecordIncomeCommandHandler>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();

        const decimal incomeAmount = 50m;
        var accountId = Guid.NewGuid();
        var command = new RecordIncomeCommand(accountId, incomeAmount, "Test income");

        // Act
        var result = await handler.Handle(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.AccountNotFound), result.Error);

        var publishedEvent = harness.Published
            .Select<MoneyDeposited>()
            .FirstOrDefault(e => e.Context.Message.AccountId == accountId)?
            .Context.Message;

        Assert.Null(publishedEvent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task Handle_ShouldReturnFailure_WhenInvalidAmount(decimal invalidIncomeAmount)
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AccountDbContext>>();
        var handler = scope.ServiceProvider.GetRequiredService<RecordIncomeCommandHandler>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();

        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        var command = new RecordIncomeCommand(accountId, invalidIncomeAmount, "Test income");

        await using (var writeContext = await dbContextFactory.CreateDbContextAsync())
        {
            var newAccount = new AssetAccount(accountId, ownerIds);

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
            // Balance remains unchanged.
            Assert.Equal(0, account.Balance);
            // No deposit transaction should be recorded.
            Assert.Empty(account.Transactions);
        }

        var publishedEvent = harness.Published
            .Select<MoneyDeposited>()
            .FirstOrDefault(e => e.Context.Message.AccountId == accountId)?
            .Context.Message;

        Assert.Null(publishedEvent);
    }

    [Fact]
    public async Task Handle_ShouldDepositMultipleIncomes_WhenCalledMultipleTimes()
    {
        // Arrange
        using var scope = fixture.ServiceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AccountDbContext>>();
        var handler = scope.ServiceProvider.GetRequiredService<RecordIncomeCommandHandler>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();

        decimal[] incomeAmounts = [50m, 30m];
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };

        await using (var writeContext = await dbContextFactory.CreateDbContextAsync())
        {
            var newAccount = new AssetAccount(accountId, ownerIds);

            writeContext.AssetAccounts.Add(newAccount);

            await writeContext.SaveChangesAsync();
        }

        var commands = incomeAmounts.Select((amount, index) =>
            new RecordIncomeCommand(accountId, amount, $"Income {index + 1}"));

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
            Assert.Equal(incomeAmounts.Sum(), account.Balance);
            Assert.Equal(incomeAmounts.Length, account.Transactions.Count);
        }

        var publishedEvents = harness.Published
            .Select<MoneyDeposited>()
            .Where(e => e.Context.Message.AccountId == accountId)
            .Select(e => e.Context.Message)
            .ToArray();

        Assert.Equal(incomeAmounts.Length, publishedEvents.Length);
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

        const decimal incomeAmount = 30m;
        const int numConcurrentTransactions = 5;
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };

        await using (var writeContext = await dbContextFactory.CreateDbContextAsync())
        {
            var newAccount = new AssetAccount(accountId, ownerIds);

            writeContext.AssetAccounts.Add(newAccount);

            await writeContext.SaveChangesAsync();
        }

        // Act
        // Simulate concurrent income transactions by creating a new scope per task.
        var tasks = Enumerable.Range(0, numConcurrentTransactions)
            .Select(i => Task.Run(async () =>
            {
                using var innerScope = fixture.ServiceProvider.CreateScope();
                var handler = innerScope.ServiceProvider.GetRequiredService<RecordIncomeCommandHandler>();

                return await handler.Handle(
                    new RecordIncomeCommand(accountId, incomeAmount, $"Simultaneous income {i + 1}"));
            }));

        var results = await Task.WhenAll(tasks);

        // All transactions should succeed.
        Assert.All(results, result => Assert.True(result.IsSuccess));

        await using (var readContext = await dbContextFactory.CreateDbContextAsync())
        {
            var account = await readContext.AssetAccounts
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            Assert.NotNull(account);

            const decimal expectedBalance = numConcurrentTransactions * incomeAmount;
            Assert.Equal(expectedBalance, account.Balance);
            Assert.Equal(numConcurrentTransactions, account.Transactions.Count);
        }

        var publishedEvents = harness.Published
            .Select<MoneyDeposited>()
            .Where(e => e.Context.Message.AccountId == accountId)
            .Select(e => e.Context.Message)
            .ToArray();

        Assert.Equal(numConcurrentTransactions, publishedEvents.Length);
        Assert.All(publishedEvents, msg => Assert.Equal(incomeAmount, msg.Amount));
    }
}