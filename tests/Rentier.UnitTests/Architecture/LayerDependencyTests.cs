using System.Reflection;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Rentier.UnitTests.Architecture;

/// <summary>
/// Desktop is the composition root, so the Desktop <em>assembly</em> references Infrastructure.
/// The rule that matters is per type: only the bootstrap code (Composition/, Program, App) may
/// touch Infrastructure; ViewModels, Views, Services, Converters, Dialogs etc. must go through
/// Application abstractions.
/// </summary>
public class LayerDependencyTests
{
    private const string DOMAIN_ASSEMBLY = "Rentier.Domain";
    private const string APPLICATION_ASSEMBLY = "Rentier.Application";
    private const string INFRASTRUCTURE_ASSEMBLY = "Rentier.Infrastructure";
    private const string DESKTOP_ASSEMBLY = "Rentier.Desktop";
    private const string COMPOSITION_NAMESPACE = "Rentier.Desktop.Composition";

    private static readonly Assembly DESKTOP = typeof(Rentier.Desktop.App).Assembly;

    /// <summary>Bootstrap types outside Composition/ that are part of the composition root.</summary>
    private static readonly Type[] BOOTSTRAP_TYPES =
    [
        typeof(Rentier.Desktop.App),
        typeof(Rentier.Desktop.Program),
    ];

    [Fact]
    public void Desktop_OutsideCompositionRoot_MustNotUse_InfrastructureTypes()
    {
        var violations = DesktopTopLevelTypes()
            .Where(type => !IsCompositionRoot(type))
            .SelectMany(type => TypeUsageScanner.Scan(type).Types
                .Where(used => AssemblyNameOf(used) == INFRASTRUCTURE_ASSEMBLY)
                .Select(used => $"{type.FullName} -> {used.FullName}"))
            .Order()
            .ToList();

        violations.Should().BeEmpty(
            "only the composition root (Rentier.Desktop.Composition, Program, App) may use " +
            "Rentier.Infrastructure types; everything else must depend on Rentier.Application abstractions");
    }

    [Fact]
    public void Desktop_CompositionRoot_Uses_InfrastructureDirectly()
    {
        // Guards the other half of the rule: Infrastructure is wired by an explicit, compiled
        // call from the composition root, not discovered at runtime.
        var compositionRootUsesInfrastructure = DesktopTopLevelTypes()
            .Where(IsCompositionRoot)
            .SelectMany(type => TypeUsageScanner.Scan(type).Types)
            .Any(used => AssemblyNameOf(used) == INFRASTRUCTURE_ASSEMBLY);

        compositionRootUsesInfrastructure.Should().BeTrue(
            "the composition root must register Infrastructure through a direct call");
    }

    [Fact]
    public void Desktop_MustNot_LoadAssembliesByReflection()
    {
        // Assembly.Load("Rentier.Infrastructure") hides a real dependency from the compiler,
        // from this test, and from trimming/packaging tools.
        var violations = DesktopTopLevelTypes()
            .SelectMany(type => TypeUsageScanner.Scan(type).Members
                .OfType<MethodBase>()
                .Where(method => method.DeclaringType == typeof(Assembly)
                                 && method.Name.StartsWith("Load", StringComparison.Ordinal))
                .Select(method => $"{type.FullName} -> Assembly.{method.Name}"))
            .Distinct()
            .Order()
            .ToList();

        violations.Should().BeEmpty("layers are wired with compile-time references, not reflection");
    }

    [Fact]
    public void Domain_MustNot_Reference_OtherLayers()
    {
        ReferencedAssemblyNames(typeof(Rentier.Domain.Entities.Filing).Assembly)
            .Should().NotContain([APPLICATION_ASSEMBLY, INFRASTRUCTURE_ASSEMBLY, DESKTOP_ASSEMBLY]);
    }

    [Fact]
    public void Application_MustNot_Reference_OuterLayers()
    {
        ReferencedAssemblyNames(typeof(Rentier.Application.Common.Error).Assembly)
            .Should().NotContain([INFRASTRUCTURE_ASSEMBLY, DESKTOP_ASSEMBLY]);
    }

    [Fact]
    public void Infrastructure_MustNot_Reference_Desktop()
    {
        var infrastructure = Assembly.Load(INFRASTRUCTURE_ASSEMBLY);

        ReferencedAssemblyNames(infrastructure).Should().NotContain(DESKTOP_ASSEMBLY);
    }

    // ── Scanner self-checks: a blind scanner would make the rules above pass vacuously ──

    [Fact]
    public void Scan_TypeUsedOnlyInsideMethodBody_IsDetected()
    {
        var usage = TypeUsageScanner.Scan(typeof(ScannerProbe));

        usage.Types.Should().Contain(typeof(StringBuilder), "it is constructed inside a method body");
        usage.Types.Should().Contain(typeof(UriBuilder), "it is used inside a lambda closure");
        usage.Types.Should().Contain(typeof(Version), "it is used after an await in an async state machine");
        usage.Types.Should().Contain(typeof(BitConverter), "a static method on it is called");
    }

    [Fact]
    public void Scan_TypeUsedOnlyInSignatures_IsDetected()
    {
        var usage = TypeUsageScanner.Scan(typeof(ScannerProbe));

        usage.Types.Should().Contain(typeof(TimeZoneInfo), "it is a field type");
        usage.Types.Should().Contain(typeof(OperatingSystem), "it is a generic argument of a parameter type");
        usage.Types.Should().Contain(typeof(ObsoleteAttribute), "it is applied as an attribute");
    }

    [Fact]
    public void Scan_MethodCall_IsReportedAsMember()
    {
        var usage = TypeUsageScanner.Scan(typeof(ScannerProbe));

        usage.Members.OfType<MethodBase>().Should().Contain(
            method => method.DeclaringType == typeof(BitConverter) && method.Name == nameof(BitConverter.GetBytes));
    }

    private static IEnumerable<Type> DesktopTopLevelTypes() =>
        DESKTOP.GetTypes().Where(type => !type.IsNested);

    private static bool IsCompositionRoot(Type type) =>
        type.Namespace == COMPOSITION_NAMESPACE
        || (type.Namespace?.StartsWith(COMPOSITION_NAMESPACE + ".", StringComparison.Ordinal) ?? false)
        || BOOTSTRAP_TYPES.Contains(type);

    private static string? AssemblyNameOf(Type type) => type.Assembly.GetName().Name;

    private static IEnumerable<string?> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name);

#pragma warning disable CS0169, CS0649, CA1822, IDE0051, IDE0044, S1144, S4487, S1172
    private sealed class ScannerProbe
    {
        private TimeZoneInfo? _fieldOnly;

        [Obsolete("probe")]
        private void SignatureOnly(List<OperatingSystem> parameter)
        {
        }

        private int LocalOnly()
        {
            var builder = new StringBuilder();
            return builder.Length + BitConverter.GetBytes(1).Length;
        }

        private Func<int> ClosureOnly(int port) => () => new UriBuilder { Port = port }.Port;

        private async Task<int> AsyncOnly()
        {
            await Task.Yield();
            return new Version(1, 0).Major;
        }
    }
#pragma warning restore CS0169, CS0649, CA1822, IDE0051, IDE0044, S1144, S4487, S1172
}
