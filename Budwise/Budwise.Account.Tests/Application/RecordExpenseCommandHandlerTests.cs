using Budwise.Account.Application.Commands;
using Budwise.Account.Application.Handlers;
using Budwise.Account.Domain.Aggregates;
using Budwise.Account.Domain.Entities;
using Budwise.Account.Domain.Errors;
using Budwise.Account.Infrastructure.Messaging;
using Budwise.Account.Infrastructure.Persistence;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

public class RecordExpenseCommandHandlerTests
{
    private readonly DbContextOptions<AccountDbContext> _dbContextOptions;
    private readonly Mock<AccountEventsPublisher> _publisherMock;

    public RecordExpenseCommandHandlerTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<AccountDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase")
            .Options;

        _publisherMock = new Mock<AccountEventsPublisher>();
    }

    [Fact]
    public async Task Handle_ShouldWithdrawAmount_WhenAccountExists()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        var command = new RecordExpenseCommand(accountId, 50m, "Test expense");

        using (var context = new AccountDbContext(_dbContextOptions))
        {
            var account = new BankAccount(accountId, ownerIds);
            account.Deposit(100m, "Initial deposit");
            context.BankAccounts.Add(account);
            await context.SaveChangesAsync();
        }

        using (var context = new AccountDbContext(_dbContextOptions))
        {
            var handler = new RecordExpenseCommandHandler(context, _publisherMock.Object);

            // Act
            var result = await handler.Handle(command);

            // Assert
            Assert.True(result.IsSuccess);

            var account = await context.BankAccounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
            Assert.NotNull(account);
            Assert.Equal(50m, account.Balance);
            Assert.Single(account.Transactions);
            Assert.Equal(TransactionType.Credit, account.Transactions[0].Type);
        }
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenAccountDoesNotExist()
    {
        // Arrange
        var command = new RecordExpenseCommand(Guid.NewGuid(), 50m, "Test expense");

        using (var context = new AccountDbContext(_dbContextOptions))
        {
            var handler = new RecordExpenseCommandHandler(context, _publisherMock.Object);

            // Act
            var result = await handler.Handle(command);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorMessage.FromCode(ErrorCode.AccountNotFound), result.Error);
        }
    }
}