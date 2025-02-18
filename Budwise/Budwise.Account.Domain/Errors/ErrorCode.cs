namespace Budwise.Account.Domain.Errors;

public enum ErrorCode
{
    EmptyAccountId = 1000,
    EmptyOwnerIds,
    OwnerIdsContainEmpty,
    
    InvalidAmount = 2000,
    InsufficientFunds,
    AccountNotFound
}