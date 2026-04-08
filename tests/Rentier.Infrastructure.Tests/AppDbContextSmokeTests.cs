using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Rentier.Infrastructure.Persistence;
using Xunit;

namespace Rentier.Infrastructure.Tests;

public class AppDbContextSmokeTests
{
    [Fact]
    public void AppDbContext_CreatedWithSqliteMemory_DoesNotThrow()
    {
        var act = () =>
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;

            using var context = new AppDbContext(options);
            context.Database.EnsureCreated();
        };

        act.Should().NotThrow();
    }
}
