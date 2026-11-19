using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Rentier.UnitTests.Architecture;

/// <summary>
/// EF Core's <c>EnableSensitiveDataLogging</c> makes the framework write parameter values — amounts,
/// names, ids — into every logged command. The diagnostic log must stay safe to share, so no
/// production assembly may switch it on. The check inspects compiled IL, so it also covers lambdas
/// (such as the <c>AddDbContext</c> options callback). Matching is by method name so this test
/// needs no EF Core reference.
/// </summary>
public class SensitiveLoggingTests
{
    private const string FORBIDDEN_METHOD = "EnableSensitiveDataLogging";

    public static TheoryData<string> GuardedAssemblies => new()
    {
        typeof(Rentier.Domain.Entities.Report).Assembly.GetName().Name!,
        typeof(Rentier.Application.Common.Error).Assembly.GetName().Name!,
        typeof(Rentier.Desktop.App).Assembly.GetName().Name!,
        "Rentier.Infrastructure",
    };

    [Theory]
    [MemberData(nameof(GuardedAssemblies))]
    public void Assembly_MustNot_EnableEfCoreSensitiveDataLogging(string assemblyName)
    {
        var offenders = FindCallers(Assembly.Load(assemblyName));

        offenders.Should().BeEmpty(
            "{0} must not call {1}: it would put parameter values (amounts, names) into the shareable log",
            assemblyName, FORBIDDEN_METHOD);
    }

    [Fact]
    public void Detector_TypeCallingForbiddenMethod_IsReported()
    {
        // Guards the guard: if the IL scan stops recognising calls, this fails.
        var offenders = FindCallers(typeof(SensitiveLoggingTests).Assembly);

        offenders.Should().Contain(o => o.Contains(nameof(SensitiveCallerSample)));
    }

    private sealed class FakeOptionsBuilder
    {
        // Same name as the real EF Core method; matched by name only.
        public FakeOptionsBuilder EnableSensitiveDataLogging() => this;
    }

    private static class SensitiveCallerSample
    {
        public static object Configure() => new FakeOptionsBuilder().EnableSensitiveDataLogging();
    }

    private static List<string> FindCallers(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => type != typeof(FakeOptionsBuilder))
            .Where(type => TypeUsageScanner.Scan(type).Members
                .Any(member => member is MethodBase { Name: FORBIDDEN_METHOD }))
            .Select(type => type.FullName ?? type.Name)
            .Order()
            .ToList();
}
