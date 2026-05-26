using FluentAssertions;
using Xunit;

namespace Rentier.UnitTests.Architecture;

public class LayerDependencyTests
{
    [Fact]
    public void Desktop_MustNot_Reference_Infrastructure()
    {
        var desktopAssembly = typeof(Rentier.Desktop.App).Assembly;
        var referencedNames = desktopAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name);

        referencedNames.Should().NotContain("Rentier.Infrastructure");
    }
}
