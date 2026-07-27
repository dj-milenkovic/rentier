using System.Reflection;
using FluentAssertions;
using Rentier.Infrastructure.Updates;

namespace Rentier.Infrastructure.Tests.Updates;

public class UpdateFeedMetadataTests
{
    [Fact]
    public void InfrastructureAssembly_UpdateFeedUrlMetadata_PointsAtCanonicalRepository()
    {
        var value = typeof(VelopackManagerAdapter).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "UpdateFeedUrl")?
            .Value;

        value.Should().Be("https://github.com/dj-milenkovic/rentier");
    }
}
