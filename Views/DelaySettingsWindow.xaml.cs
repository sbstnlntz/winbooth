// Offers sliders for tuning capture delays, countdowns, and idle timers.

using System;
using System.Windows;
using winbooth.ViewModels;

namespace winbooth.Views
{
    public partial class DelaySettingsWindow : Window
    {
        public DelaySettingsWindow(StartViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
