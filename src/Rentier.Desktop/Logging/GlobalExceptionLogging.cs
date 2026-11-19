using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Avalonia.Threading;
using Serilog;

namespace Rentier.Desktop.Logging;

/// <summary>
/// Hooks the process-wide "nobody caught this" paths so crashes leave a trace in the log.
/// None of the hooks changes behaviour: they log, then let the existing handling continue.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Process-level wiring; exercised by running the app.")]
internal static class GlobalExceptionLogging
{
    public static void Attach()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})", e.IsTerminating);
            if (e.IsTerminating)
                Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
            Log.Error(e.Exception, "Unobserved task exception");

        Dispatcher.UIThread.UnhandledException += (_, e) =>
            Log.Fatal(e.Exception, "Unhandled UI-thread exception");
    }

    /// <summary>
    /// Exception sink for ReactiveUI (replaces its default handler). Logs, then rethrows on the
    /// UI thread so the app fails exactly as it did with the default handler.
    /// </summary>
    internal sealed class ReactiveExceptionObserver : IObserver<Exception>
    {
        public void OnNext(Exception value)
        {
            Log.Fatal(value, "Unhandled ReactiveUI exception");
            Dispatcher.UIThread.Post(() => ExceptionDispatchInfo.Capture(value).Throw());
        }

        public void OnError(Exception error)
        {
            Log.Fatal(error, "ReactiveUI exception stream failed");
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        public void OnCompleted()
        {
        }
    }
}
