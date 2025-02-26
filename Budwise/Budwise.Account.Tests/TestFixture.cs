using Budwise.Account.Application.Handlers;
using Budwise.Account.Infrastructure.Messaging;
using Budwise.Account.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Budwise.Account.Tests;

public class TestFixture : IAsyncLifetime
{
    public ServiceProvider ServiceProvider { get; }

    public TestFixture()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AccountDbContext>(options => 
            options.UseNpgsql("Host=localhost;Database=testdb;Username=testuser;Password=testpassword"));

        services.AddMassTransitTestHarness();
        services.AddScoped<AccountEventsPublisher>();
        services.AddScoped<RecordExpenseCommandHandler>();
        services.AddScoped<RecordIncomeCommandHandler>();

        ServiceProvider = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        var harness = ServiceProvider.GetRequiredService<ITestHarness>();
        await harness.Start();
    }

    public async Task DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
    }
}