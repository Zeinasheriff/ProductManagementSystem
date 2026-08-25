using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProductManagement.Infrastructure.Persistence;

namespace ProductManagement.UnitTests.Helpers;

/// <summary>
/// Creates isolated, fully functional ApplicationDbContext instances backed by
/// the EF Core InMemory provider. Transactions are ignored by the provider,
/// which mirrors how OrderService's BeginTransactionAsync behaves in tests.
/// </summary>
public static class TestDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }
}