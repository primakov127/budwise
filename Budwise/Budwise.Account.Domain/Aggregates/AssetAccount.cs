﻿using CSharpFunctionalExtensions;
using Budwise.Account.Domain.Entities;
using Budwise.Account.Domain.Errors;
using CSharpFunctionalExtensions.ValueTasks;

namespace Budwise.Account.Domain.Aggregates
{
    public class AssetAccount
    {
        public Guid AccountId { get; private set; }
        public decimal Balance { get; private set; }
        public IReadOnlyList<Guid> OwnerIds { get; private set; }
        public IReadOnlyList<Transaction> Transactions => _transactions.AsReadOnly();
        public byte[] Version { get; private set; }
        
        private readonly List<Transaction> _transactions;

        private AssetAccount()
        {
            Balance = 0;
            _transactions = [];
            
            UpdateVersion();
        }
        
        public AssetAccount(Guid accountId, List<Guid> ownerIds) : this()
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
                .Tap(() => AddTransaction(amount, TransactionType.Debit, note))
                .Tap(UpdateVersion);
            
        public Result Withdraw(decimal amount, string? note) =>
            ValidateAmount(amount)
                .Bind(() => EnsureSufficientFunds(amount))
                .Tap(() => Balance -= amount)
                .Tap(() => AddTransaction(amount, TransactionType.Credit, note))
                .Tap(UpdateVersion);

        public Result Transfer(decimal amount, AssetAccount? destinationAccount) =>
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
        
        private void UpdateVersion()
        {
            Version = Guid.NewGuid().ToByteArray();
        }
        
        private static Result ValidateAmount(decimal amount) =>
            amount > 0 
                ? Result.Success() 
                : Result.Failure(ErrorMessage.FromCode(ErrorCode.InvalidAmount));

        private Result EnsureSufficientFunds(decimal amount) =>
            Balance >= amount 
                ? Result.Success() 
                : Result.Failure(ErrorMessage.FromCode(ErrorCode.InsufficientFunds));

        private static Result ValidateDestinationAccount(AssetAccount? account) =>
            account is not null 
                ? Result.Success() 
                : Result.Failure(ErrorMessage.FromCode(ErrorCode.AccountNotFound));
    }
}
