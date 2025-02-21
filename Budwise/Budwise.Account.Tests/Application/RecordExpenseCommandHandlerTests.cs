using Budwise.Account.Application.Commands;
using Budwise.Account.Application.Handlers;
using Budwise.Account.Domain.Aggregates;
using Budwise.Account.Domain.Entities;
using Budwise.Account.Domain.Events;
using Budwise.Account.Infrastructure.Messaging;
using Budwise.Account.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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

    public Task DisposeAsync()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Handle_ShouldWithdrawAmount_WhenAccountExists()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        var command = new RecordExpenseCommand(accountId, 50m, "Test expense");

        var newAccount = new BankAccount(accountId, ownerIds);
        newAccount.Deposit(100m, "Initial deposit");
        _context.BankAccounts.Add(newAccount);
        await _context.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(command);

        // Assert
        Assert.True(result.IsSuccess);

        var account = await _context.BankAccounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.AccountId == accountId);
        
        Assert.NotNull(account);
        Assert.Equal(50m, account.Balance);
        Assert.Equal(2, account.Transactions.Count);
        Assert.Equal(TransactionType.Credit, account.Transactions[1].Type);

        Assert.True(await _harness.Published.Any<MoneyWithdrawn>());
    }

    // [Fact]
    // public async Task Handle_ShouldReturnFailure_WhenAccountDoesNotExist()
    // {
    //     // Arrange
    //     var command = new RecordExpenseCommand(Guid.NewGuid(), 50m, "Test expense");
    //
    //     using (var context = new AccountDbContext(_dbContextOptions))
    //     {
    //         var handler = new RecordExpenseCommandHandler(context, _publisherMock.Object);
    //
    //         // Act
    //         var result = await handler.Handle(command);
    //
    //         // Assert
    //         Assert.True(result.IsFailure);
    //         Assert.Equal(ErrorMessage.FromCode(ErrorCode.AccountNotFound), result.Error);
    //     }
    // }
}