#nullable enable

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using FotoboxApp.ViewModels;
using FotoboxApp.Views;

namespace FotoboxApp;

public partial class MainWindow : Window
{
    private readonly StartViewModel _mainViewModel = new StartViewModel();

    public MainWindow()
    {
        InitializeComponent();

        MainFrame.Navigate(new StartView(_mainViewModel)); // always reuse the same instance

        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    // Property so all views can access the shared StartViewModel instance.
    public StartViewModel MainViewModel => _mainViewModel;

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            && (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            && e.Key == Key.Q)
        {
            // Navigate back to the start view with the same view model.
            MainFrame.Navigate(new StartView(_mainViewModel));
            e.Handled = true;
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AdjustWindowToWorkArea();
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
            or nameof(SystemParameters.FullPrimaryScreenWidth))
        {
            AdjustWindowToWorkArea();
        }
    }

    private void AdjustWindowToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;

        MaxWidth = workArea.Width;
        MaxHeight = workArea.Height;
        Width = workArea.Width;
        Height = workArea.Height;
        Left = workArea.Left;
        Top = workArea.Top;
    }
}
