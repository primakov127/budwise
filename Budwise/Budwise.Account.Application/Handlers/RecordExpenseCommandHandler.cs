using Budwise.Account.Application.Commands;
using Budwise.Account.Domain.Errors;
using Budwise.Account.Domain.Events;
using Budwise.Account.Infrastructure.Messaging;
using Budwise.Account.Infrastructure.Persistence;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace Budwise.Account.Application.Handlers;

public class RecordExpenseCommandHandler(AccountDbContext context, AccountEventsPublisher publisher)
{
    private static readonly AsyncRetryPolicy RetryPolicy = Policy
        .Handle<DbUpdateConcurrencyException>()
        .WaitAndRetryAsync(new[]
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(3)
        });
    
    public async Task<Result> Handle(RecordExpenseCommand command)
    {
        return await RetryPolicy
            .ExecuteAsync(async () => await ProcessWithdrawal(command))
            .Tap(async () => await publisher.PublishMoneyWithdrawn(
                new MoneyWithdrawn(command.AccountId, command.Amount))
            );
    }
    
    private async Task<Result> ProcessWithdrawal(RecordExpenseCommand command)
    {
        var account = await context.AssetAccounts.FirstOrDefaultAsync(a => a.AccountId == command.AccountId);
        
        return await Result.FailureIf(account is null, ErrorMessage.FromCode(ErrorCode.AccountNotFound))
            .Bind(() => account!.Withdraw(command.Amount, command.Note))
            .Tap(async () => await Save());
    }

    private async Task<Result> Save()
    {
        try
        {
            await context.SaveChangesAsync();
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}