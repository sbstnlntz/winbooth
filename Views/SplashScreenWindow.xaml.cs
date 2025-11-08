#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace FotoboxApp.Views;

public partial class SplashScreenWindow : Window
{
    private const double AutoProgressTarget = 90d;
    private const string DisplayVersion = "0.410";

    private CancellationTokenSource? _autoProgressCts;
    private CancellationTokenRegistration? _autoProgressCancellationRegistration;
    private TaskCompletionSource<object?>? _autoProgressCompletion;
    private DoubleAnimation? _autoProgressAnimation;

    private double _currentProgress;

    public SplashScreenWindow()
    {
        InitializeComponent();

        Loaded += SplashScreenWindow_OnLoaded;
        Closed += SplashScreenWindow_OnClosed;
    }

    public Task RunAutomaticProgressAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        StopAutoProgress(canceled: true);

        if (duration <= TimeSpan.Zero)
        {
            UpdateProgress(AutoProgressTarget);
            return Task.CompletedTask;
        }

        _currentProgress = LoadingProgressBar.Value;
        _autoProgressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _autoProgressCts.Token;

        _autoProgressCompletion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _autoProgressCancellationRegistration = token.CanBeCanceled
            ? token.Register(() => Dispatcher.Invoke(() => StopAutoProgress(canceled: true)))
            : null;

        _autoProgressAnimation = new DoubleAnimation
        {
            From = _currentProgress,
            To = AutoProgressTarget,
            Duration = duration,
            FillBehavior = FillBehavior.HoldEnd,
            EasingFunction = new QuadraticEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };
        _autoProgressAnimation.Completed += AutoProgressAnimationCompleted;

        LoadingProgressBar.BeginAnimation(RangeBase.ValueProperty, _autoProgressAnimation);

        return _autoProgressCompletion.Task;
    }

    public void UpdateStatus(string status) => StatusTextBlock.Text = status;

    public void UpdateProgress(double value)
    {
        var clamped = Math.Max(0, Math.Min(100, value));
        if (clamped < _currentProgress)
        {
            return;
        }

        LoadingProgressBar.BeginAnimation(RangeBase.ValueProperty, null);

        _currentProgress = clamped;
        LoadingProgressBar.Value = clamped;
    }

    public void CompleteProgress()
    {
        StopAutoProgress(canceled: false);
        UpdateProgress(100);
    }

    private void AutoProgressAnimationCompleted(object? sender, EventArgs e)
    {
        _currentProgress = LoadingProgressBar.Value;
        StopAutoProgress(canceled: false);
    }

    private void StopAutoProgress(bool canceled)
    {
        if (_autoProgressAnimation is not null)
        {
            _autoProgressAnimation.Completed -= AutoProgressAnimationCompleted;
            LoadingProgressBar.BeginAnimation(RangeBase.ValueProperty, null);
            _autoProgressAnimation = null;
        }

        _currentProgress = LoadingProgressBar.Value;

        _autoProgressCts?.Dispose();
        _autoProgressCts = null;
        _autoProgressCancellationRegistration?.Dispose();
        _autoProgressCancellationRegistration = null;

        if (_autoProgressCompletion is null)
        {
            return;
        }

        if (canceled)
        {
            _autoProgressCompletion.TrySetCanceled();
        }
        else
        {
            _autoProgressCompletion.TrySetResult(null);
        }

        _autoProgressCompletion = null;
    }

    private void SplashScreenWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        AdjustWindowToFullScreen();
        SystemParameters.StaticPropertyChanged += SystemParametersOnStaticPropertyChanged;

        var currentYear = DateTime.Now.Year;
        var copyright = currentYear > 2025
            ? $"\u00A9 2025 - {currentYear} Sebastian Lentz"
            : "\u00A9 2025 Sebastian Lentz";

        FooterCopyrightText.Text = copyright;
        FooterVersionText.Text = $"Version {DisplayVersion}";
    }

    private void SplashScreenWindow_OnClosed(object? sender, EventArgs e)
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
