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
    // One crash travels through several hooks (ReactiveUI → UI thread → AppDomain). The first
    // hook to log it tags the exception so the later ones stay silent.
    private const string LOGGED_MARKER = "Rentier.Logging.Logged";

    public static void Attach()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (MarkLogged(e.ExceptionObject as Exception))
                Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})", e.IsTerminating);
            if (e.IsTerminating)
                Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            if (MarkLogged(e.Exception))
                Log.Error(e.Exception, "Unobserved task exception");
        };

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            if (MarkLogged(e.Exception))
                Log.Fatal(e.Exception, "Unhandled UI-thread exception");
        };
    }

    /// <summary>
    /// Tags <paramref name="exception"/> as logged. Returns <c>false</c> when an earlier hook
    /// already logged it. Untaggable or null exceptions are always logged.
    /// </summary>
    internal static bool MarkLogged(Exception? exception)
    {
        if (exception is null)
            return true;

        try
        {
            if (exception.Data.Contains(LOGGED_MARKER))
                return false;
            exception.Data[LOGGED_MARKER] = true;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
        {
            // Read-only or restricted Data dictionary: logging twice beats not logging.
        }

        return true;
    }

    /// <summary>
    /// Exception sink for ReactiveUI (replaces its default handler). Logs, then rethrows on the
    /// UI thread so the app fails exactly as it did with the default handler.
    /// </summary>
    internal sealed class ReactiveExceptionObserver : IObserver<Exception>
    {
        public void OnNext(Exception value)
        {
            if (MarkLogged(value))
                Log.Fatal(value, "Unhandled ReactiveUI exception");
            Dispatcher.UIThread.Post(() => ExceptionDispatchInfo.Capture(value).Throw());
        }

        public void OnError(Exception error)
        {
            if (MarkLogged(error))
                Log.Fatal(error, "ReactiveUI exception stream failed");
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        public void OnCompleted()
        {
        }
    }
}
