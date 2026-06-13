namespace Rentier.UnitTests.Application.Common;

/// <summary>
/// A synchronous IProgress&lt;T&gt; that invokes the callback inline on the calling
/// thread, preserving report order and eliminating race conditions in unit tests.
/// </summary>
internal sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly List<T> _entries = [];
    public IReadOnlyList<T> Entries => _entries;

    public void Report(T value) => _entries.Add(value);
}
