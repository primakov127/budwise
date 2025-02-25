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

public class RecordIncomeCommandHandler(IDbContextFactory<AccountDbContext> contextFactory, AccountEventsPublisher publisher)
{
    private static readonly AsyncRetryPolicy RetryPolicy = Policy
        .Handle<DbUpdateConcurrencyException>()
        .RetryAsync(3);
    
    public Task<Result> Handle(RecordIncomeCommand command) => RetryPolicy
        .ExecuteAsync(async () => await ProcessDeposit(command))
        .Tap(async () => await publisher.PublishMoneyDeposited(
            new MoneyDeposited(command.AccountId, command.Amount))
        );
    
    private async Task<Result> ProcessDeposit(RecordIncomeCommand command)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var account = await context.AssetAccounts.FirstOrDefaultAsync(a => a.AccountId == command.AccountId);
        
        return await Result.FailureIf(account is null, ErrorMessage.FromCode(ErrorCode.AccountNotFound))
            .Bind(() => account!.Deposit(command.Amount, command.Note))
            .Tap(async () => await context.SaveChangesAsync());
    }
}