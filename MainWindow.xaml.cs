#nullable enable

using System;
using System.ComponentModel;
using System.Windows;
using winbooth.ViewModels;
using winbooth.Views;

namespace winbooth;

public partial class MainWindow : Window
{
    private readonly StartViewModel _mainViewModel = new StartViewModel();

    public MainWindow()
    {
        InitializeComponent();

        MainFrame.Navigate(new StartView(_mainViewModel)); // always reuse the same instance

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    // Property so all views can access the shared StartViewModel instance.
    public StartViewModel MainViewModel => _mainViewModel;

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AdjustWindowToFullScreen();
        SystemParameters.StaticPropertyChanged += SystemParametersOnStaticPropertyChanged;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= SystemParametersOnStaticPropertyChanged;
    }

    private void SystemParametersOnStaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SystemParameters.WorkArea)
            or nameof(SystemParameters.FullPrimaryScreenHeight)
            or nameof(SystemParameters.FullPrimaryScreenWidth)
            or nameof(SystemParameters.PrimaryScreenHeight)
            or nameof(SystemParameters.PrimaryScreenWidth))
        {
            AdjustWindowToFullScreen();
        }
    }

    private void AdjustWindowToFullScreen()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        WindowState = WindowState.Normal;

        MaxWidth = screenWidth;
        MaxHeight = screenHeight;
        Width = screenWidth;
        Height = screenHeight;
        Left = 0;
        Top = 0;
    }
}
