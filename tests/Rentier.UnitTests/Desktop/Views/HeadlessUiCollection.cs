using Xunit;

namespace Rentier.UnitTests.Desktop.Views;

/// <summary>
/// Serializes all [AvaloniaFact] headless tests within the UnitTests assembly.
/// The Avalonia headless platform shares a single UI thread; concurrent
/// [AvaloniaFact] calls would race on that thread and produce flaky results.
/// Tests in other collections (Domain, Application, Desktop VMs) still run
/// in full parallel — only the seven view-render tests are serialized here.
/// </summary>
[CollectionDefinition("HeadlessUI", DisableParallelization = true)]
public sealed class HeadlessUiCollection { }
