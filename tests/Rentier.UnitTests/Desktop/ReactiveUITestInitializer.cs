using System.Runtime.CompilerServices;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;

namespace Rentier.UnitTests.Desktop;

internal static class ReactiveUiTestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        if (Splat.Builder.AppBuilder.HasBeenBuilt)
            return;

        RxAppBuilder.CreateReactiveUIBuilder()
            .WithAvalonia()
            .WithExceptionHandler(new NoOpExceptionObserver())
            .BuildApp();
    }

    private sealed class NoOpExceptionObserver : IObserver<Exception>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(Exception value) { }
    }
}
