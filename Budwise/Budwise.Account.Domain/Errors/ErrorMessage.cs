namespace Budwise.Account.Domain.Errors;

public static class ErrorMessage
{
    private const string DefaultMessage = "An unknown error occurred.";
    
    private static readonly IReadOnlyDictionary<ErrorCode, string> Messages = new Dictionary<ErrorCode, string>
    {
        { ErrorCode.EmptyAccountId, "Account ID cannot be empty." },
        { ErrorCode.EmptyOwnerIds, "Owner IDs must contain at least one valid owner." },
        { ErrorCode.OwnerIdsContainEmpty, "Owner IDs cannot contain an empty GUID." },
        
        { ErrorCode.InvalidAmount, "Amount must be greater than zero." },
        { ErrorCode.InsufficientFunds, "Insufficient funds for this transaction." },
        { ErrorCode.AccountNotFound, "The specified bank account could not be found." }
    };

    public static string FromCode(ErrorCode code) => Messages.GetValueOrDefault(code, DefaultMessage);
}