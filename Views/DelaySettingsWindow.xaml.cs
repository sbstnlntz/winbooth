using System;
using System.Windows;
using FotoboxApp.ViewModels;

namespace FotoboxApp.Views
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
