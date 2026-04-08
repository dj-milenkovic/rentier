using System;
using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel vm) : this()
    {
        DataContext = vm;

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.ViewModel!.SelectedEntry)
                .Subscribe(entry =>
                {
                    if (entry is not null && ViewModel is not null)
                        ViewModel.CurrentViewModel = entry.ViewModel;
                })
                .DisposeWith(disposables);
        });
    }
}
