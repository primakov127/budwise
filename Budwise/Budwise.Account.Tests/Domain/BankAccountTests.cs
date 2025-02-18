using Budwise.Account.Domain.Aggregates;
using Budwise.Account.Domain.Entities;
using Budwise.Account.Domain.Errors;
using Xunit;

namespace Budwise.Account.Tests.Domain;

public class BankAccountTests
{
    [Fact]
    public void Constructor_ShouldInitializeAccount_WithZeroBalanceAndEmptyTransactions()
    {
        var ownerIds = new List<Guid> { Guid.NewGuid() };
        var account = new BankAccount(Guid.NewGuid(), ownerIds);

        Assert.Equal(0, account.Balance);
        Assert.NotNull(account.Transactions);
        Assert.Empty(account.Transactions);
        Assert.Equal(ownerIds, account.OwnerIds);
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenOwnerIdsIsNullOrEmpty()
    {
        Assert.Throws<ArgumentException>(() => new BankAccount(Guid.NewGuid(), null!));
        Assert.Throws<ArgumentException>(() => new BankAccount(Guid.NewGuid(), []));
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenAccountIdIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new BankAccount(Guid.Empty, [Guid.NewGuid()]));
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenOwnerIdsContainEmptyGuid()
    {
        var ownerIds = new List<Guid> { Guid.NewGuid(), Guid.Empty };
        Assert.Throws<ArgumentException>(() => new BankAccount(Guid.NewGuid(), ownerIds));
    }

    [Fact]
    public void Deposit_ShouldIncreaseBalanceAndAddTransaction_WhenAmountIsValid()
    {
        var account = new BankAccount(Guid.NewGuid(), [Guid.NewGuid()]);
        var result = account.Deposit(100m, "Initial deposit");

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, account.Balance);
        Assert.Single(account.Transactions);
        Assert.Equal(TransactionType.Debit, account.Transactions[0].Type);
    }

    [Fact]
    public void Deposit_ShouldFail_WhenAmountIsZeroOrNegative()
    {
        var account = new BankAccount(Guid.NewGuid(), [Guid.NewGuid()]);
        var result = account.Deposit(0m, "Invalid deposit");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.InvalidAmount), result.Error);
    }

    [Fact]
    public void Withdraw_ShouldDecreaseBalanceAndAddTransaction_WhenSufficientFunds()
    {
        var account = new BankAccount(Guid.NewGuid(), [Guid.NewGuid()]);
        account.Deposit(200m, "Initial deposit");
        var result = account.Withdraw(50m, "ATM withdrawal");

        Assert.True(result.IsSuccess);
        Assert.Equal(150m, account.Balance);
        Assert.Equal(2, account.Transactions.Count);
        Assert.Equal(TransactionType.Credit, account.Transactions[1].Type);
    }

    [Fact]
    public void Withdraw_ShouldFail_WhenInsufficientFunds()
    {
        var account = new BankAccount(Guid.NewGuid(), [Guid.NewGuid()]);
        var result = account.Withdraw(50m, "Attempted withdrawal");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.InsufficientFunds), result.Error);
    }

    [Fact]
    public void Withdraw_ShouldFail_WhenAmountIsZeroOrNegative()
    {
        var account = new BankAccount(Guid.NewGuid(), [Guid.NewGuid()]);
        var result = account.Withdraw(-10m, "Invalid withdrawal");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.InvalidAmount), result.Error);
    }

    [Fact]
    public void Transfer_ShouldMoveFunds_WhenBothAccountsAreValid()
    {
        var sourceAccount = new BankAccount(Guid.NewGuid(), [Guid.NewGuid()]);
        var destinationAccount = new BankAccount(Guid.NewGuid(), [Guid.NewGuid()]);
        sourceAccount.Deposit(300m, "Funding source account");

        var result = sourceAccount.Transfer(100m, destinationAccount);

        Assert.True(result.IsSuccess);
        Assert.Equal(200m, sourceAccount.Balance);
        Assert.Equal(100m, destinationAccount.Balance);
        Assert.Equal(2, sourceAccount.Transactions.Count);
        Assert.Single(destinationAccount.Transactions);
    }

    [Fact]
    public void Transfer_ShouldFail_WhenInsufficientFunds()
    {
        var sourceAccount = new BankAccount(Guid.NewGuid(), [Guid.NewGuid()]);
        var destinationAccount = new BankAccount(Guid.NewGuid(), [Guid.NewGuid()]);

        var result = sourceAccount.Transfer(100m, destinationAccount);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.InsufficientFunds), result.Error);
    }

    [Fact]
    public void Transfer_ShouldFail_WhenDestinationAccountIsNull()
    {
        var sourceAccount = new BankAccount(Guid.NewGuid(), [Guid.NewGuid()]);
        sourceAccount.Deposit(100m, "Funding source account");

        var result = sourceAccount.Transfer(50m, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorMessage.FromCode(ErrorCode.AccountNotFound), result.Error);
    }
}