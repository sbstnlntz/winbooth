#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace FotoboxApp.Views;

public partial class SplashScreenWindow : Window
{
    private const double AutoProgressTarget = 90d;
    private const string DisplayVersion = "0.1";

    private CancellationTokenSource? _autoProgressCts;
    private CancellationTokenRegistration? _autoProgressCancellationRegistration;
    private TaskCompletionSource<object?>? _autoProgressCompletion;
    private DoubleAnimation? _autoProgressAnimation;

    private double _currentProgress;

    public SplashScreenWindow()
    {
        InitializeComponent();
        Loaded += SplashScreenWindow_OnLoaded;
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
        var currentYear = DateTime.Now.Year;
        var copyright = currentYear > 2025
            ? $"© 2025 - {currentYear} Sebastian Lentz"
            : "© 2025 Sebastian Lentz";

        FooterCopyrightText.Text = copyright;
        FooterVersionText.Text = $"Version {DisplayVersion}";
    }
}
