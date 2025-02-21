using Budwise.Account.Application.Commands;
using Budwise.Account.Domain.Errors;
using Budwise.Account.Domain.Events;
using Budwise.Account.Infrastructure.Messaging;
using Budwise.Account.Infrastructure.Persistence;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;

namespace Budwise.Account.Application.Handlers;

public class RecordIncomeCommandHandler(AccountDbContext context, AccountEventsPublisher publisher)
{
    public async Task<Result> Handle(RecordIncomeCommand command)
    {
        var account = await context.BankAccounts.FirstOrDefaultAsync(a => a.AccountId == command.AccountId);
        if (account is null)
        {
            return Result.Failure(ErrorMessage.FromCode(ErrorCode.AccountNotFound));
        }

        return await account.Deposit(command.Amount, command.Note)
            .Tap(async () => await context.SaveChangesAsync())
            .Tap(async () => await publisher.PublishMoneyDeposited(new MoneyDeposited(command.AccountId, command.Amount)));
    }
}