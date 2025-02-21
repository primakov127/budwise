using CSharpFunctionalExtensions;
using Budwise.Account.Domain.Entities;
using Budwise.Account.Domain.Errors;

namespace Budwise.Account.Domain.Aggregates
{
    public class BankAccount
    {
        public Guid AccountId { get; private set; }
        public decimal Balance { get; private set; }
        public IReadOnlyList<Guid> OwnerIds { get; private set; }
        public IReadOnlyList<Transaction> Transactions => _transactions.AsReadOnly();
        
        private readonly List<Transaction> _transactions;

        private BankAccount()
        {
            Balance = 0;
            _transactions = [];
        }
        
        public BankAccount(Guid accountId, List<Guid> ownerIds) : this()
        {
            if (accountId == Guid.Empty)
            {
                throw new ArgumentException(ErrorMessage.FromCode(ErrorCode.EmptyAccountId), nameof(accountId));
            }

            if (ownerIds is null || ownerIds.Count == 0)
            {
                throw new ArgumentException(ErrorMessage.FromCode(ErrorCode.EmptyOwnerIds), nameof(ownerIds));
            }

            if (ownerIds.Any(id => id == Guid.Empty))
            {
                throw new ArgumentException(ErrorMessage.FromCode(ErrorCode.OwnerIdsContainEmpty), nameof(ownerIds));
            }
            
            AccountId = accountId;
            OwnerIds = ownerIds;
        }
        
        public Result Deposit(decimal amount, string? note) =>
            ValidateAmount(amount)
                .Tap(() => Balance += amount)
                .Tap(() => AddTransaction(amount, TransactionType.Debit, note));
            
        public Result Withdraw(decimal amount, string? note) =>
            ValidateAmount(amount)
                .Bind(() => EnsureSufficientFunds(amount))
                .Tap(() => Balance -= amount)
                .Tap(() => AddTransaction(amount, TransactionType.Credit, note));
        
        public Result Transfer(decimal amount, BankAccount? destinationAccount) =>
            ValidateDestinationAccount(destinationAccount)
                .Bind(() => Withdraw(amount, $"Transfer to account {destinationAccount!.AccountId}"))
                .Bind(() => destinationAccount!.Deposit(amount, $"Transfer from account {AccountId}"));
        
        private void AddTransaction(decimal amount, TransactionType type, string? note) =>
            _transactions.Add(new Transaction(
                Guid.NewGuid(),
                AccountId,
                amount,
                DateTime.UtcNow,
                type,
                note)
            );
        
        private static Result ValidateAmount(decimal amount) =>
            amount > 0 
                ? Result.Success() 
                : Result.Failure(ErrorMessage.FromCode(ErrorCode.InvalidAmount));

        private Result EnsureSufficientFunds(decimal amount) =>
            Balance >= amount 
                ? Result.Success() 
                : Result.Failure(ErrorMessage.FromCode(ErrorCode.InsufficientFunds));

        private static Result ValidateDestinationAccount(BankAccount? account) =>
            account is not null 
                ? Result.Success() 
                : Result.Failure(ErrorMessage.FromCode(ErrorCode.AccountNotFound));
    }
}
