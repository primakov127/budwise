using Budwise.Account.Domain.Aggregates;
using Budwise.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Budwise.Account.Infrastructure.Persistence;

public class AccountDbContext(DbContextOptions<AccountDbContext> options) : DbContext(options)
{
    public DbSet<BankAccount> BankAccounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BankAccount>()
            .HasKey(a => a.AccountId);

        modelBuilder.Entity<Transaction>()
            .HasKey(t => t.TransactionId);
    }
}