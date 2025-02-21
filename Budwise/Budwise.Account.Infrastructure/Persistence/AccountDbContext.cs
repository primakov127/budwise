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
        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.HasKey(a => a.AccountId);
            entity.Property(a => a.AccountId)
                .ValueGeneratedNever();
        
            entity.Property(a => a.Balance)
                .HasPrecision(18, 2)
                .IsRequired();
            
            entity.Property(a => a.OwnerIds)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(Guid.Parse)
                        .ToList()
                )
                .IsRequired();

            entity.HasMany(a => a.Transactions)
                .WithOne()
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(a => a.Transactions)
                .HasField("_transactions")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.TransactionId);
            entity.Property(t => t.TransactionId)
                .ValueGeneratedNever();
        
            entity.Property(t => t.Amount)
                .HasPrecision(18, 2)
                .IsRequired();
            
            entity.Property(t => t.Date)
                .IsRequired();
            
            entity.Property(t => t.Type)
                .IsRequired()
                .HasConversion<string>();
            
            entity.Property(t => t.Note)
                .HasMaxLength(500);
        });
    }
}