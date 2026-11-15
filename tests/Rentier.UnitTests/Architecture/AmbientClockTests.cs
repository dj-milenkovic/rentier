using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Rentier.UnitTests.Architecture;

/// <summary>
/// Domain and Application must not read the ambient system clock. Domain receives "today" /
/// timestamps as arguments; Application takes <see cref="TimeProvider"/> by constructor
/// injection. The check inspects compiled IL, so it also covers lambdas and async state machines.
/// </summary>
public class AmbientClockTests
{
    private const BindingFlags ALL_DECLARED =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.DeclaredOnly;

    private const string INSTRUMENTATION_NAMESPACE = "Coverlet.Core.Instrumentation";

    private const byte IL_CALL = 0x28;
    private const byte IL_CALLVIRT = 0x6F;

    private static readonly HashSet<MethodBase> FORBIDDEN_CLOCK_READS =
    [
        Getter(typeof(DateTime), nameof(DateTime.Now)),
        Getter(typeof(DateTime), nameof(DateTime.UtcNow)),
        Getter(typeof(DateTime), nameof(DateTime.Today)),
        Getter(typeof(DateTimeOffset), nameof(DateTimeOffset.Now)),
        Getter(typeof(DateTimeOffset), nameof(DateTimeOffset.UtcNow)),
        // TimeProvider.System is the ambient clock by another name: only the composition root may name it.
        Getter(typeof(TimeProvider), nameof(TimeProvider.System)),
    ];

    private static MethodBase Getter(Type type, string property) =>
        type.GetProperty(property, BindingFlags.Public | BindingFlags.Static)!.GetGetMethod()!;

    public static TheoryData<string> GuardedAssemblies => new()
    {
        typeof(Rentier.Domain.Entities.Report).Assembly.GetName().Name!,
        typeof(Rentier.Application.Common.Error).Assembly.GetName().Name!,
    };

    [Theory]
    [MemberData(nameof(GuardedAssemblies))]
    public void Assembly_MustNot_ReadAmbientClock(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        var offenders = FindClockReads(assembly);

        offenders.Should().BeEmpty(
            "{0} must take the current time as an argument or from an injected TimeProvider", assemblyName);
    }

    [Fact]
    public void Detector_MethodReadingAmbientClock_IsReported()
    {
        // Guards the guard: if the IL scan ever stops recognising clock reads, this fails.
        var offenders = FindClockReads(typeof(AmbientClockTests).Assembly);

        offenders.Should().Contain(o => o.Contains(nameof(ClockReadingSample)));
    }

    private static class ClockReadingSample
    {
        public static DateTime Read() => DateTime.UtcNow;
    }

    private static List<string> FindClockReads(Assembly assembly)
    {
        var offenders = new List<string>();
        foreach (var type in assembly.GetTypes())
        {
            // Coverage tooling (Coverlet) injects a Tracker type that reads the clock; it is not product code.
            if (type.FullName?.StartsWith(INSTRUMENTATION_NAMESPACE, StringComparison.Ordinal) == true) continue;
            var methods = type.GetMethods(ALL_DECLARED).Cast<MethodBase>()
                .Concat(type.GetConstructors(ALL_DECLARED));
            foreach (var method in methods)
            {
                var il = method.GetMethodBody()?.GetILAsByteArray();
                if (il is null) continue;
                offenders.AddRange(CalledMethods(method.Module, il)
                    .Where(FORBIDDEN_CLOCK_READS.Contains)
                    .Select(called => $"{type.FullName}.{method.Name} -> {called.DeclaringType!.Name}.{called.Name}"));
            }
        }
        return offenders;
    }

    /// <summary>
    /// Resolves every call/callvirt operand candidate. Scanning byte-wise (rather than decoding
    /// each opcode) can only over-report, and a stray byte pattern resolving to exactly one of
    /// the forbidden getters is not a realistic false positive.
    /// </summary>
    private static IEnumerable<MethodBase> CalledMethods(Module module, byte[] il)
    {
        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != IL_CALL && il[i] != IL_CALLVIRT) continue;
            MethodBase? called;
            try
            {
                called = module.ResolveMethod(BitConverter.ToInt32(il, i + 1));
            }
            catch (Exception ex) when (ex is ArgumentException or BadImageFormatException)
            {
                continue; // not a method token — the byte was an operand, not an opcode
            }
            if (called is not null) yield return called;
        }
    }
}
