using System.Reactive;
using System.Runtime.CompilerServices;
using ReactiveUI.Avalonia;
using ReactiveUI.Reactive.Builder;

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
            .WithExceptionHandler(Observer.Create<Exception>(_ => { }))
            .BuildApp();
    }

}
