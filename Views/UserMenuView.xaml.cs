using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using FotoboxApp.Models;
using FotoboxApp.ViewModels;
using FotoboxApp.Services;

namespace FotoboxApp.Views
{
    public partial class UserMenuView : UserControl
    {
        private readonly StartViewModel _mainViewModel;

        public UserMenuView(StartViewModel mainViewModel)
        {
            InitializeComponent();
            _mainViewModel = mainViewModel;
            DataContext = mainViewModel; // <<< WICHTIG!
            UpdateTemplateButtons();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this) as MainWindow;
            if (window != null)
                window.MainFrame.Navigate(new StartView(_mainViewModel));
        }

        private void SelectTemplateBtn1_Click(object sender, RoutedEventArgs e)
        {
            var templates = _mainViewModel.Templates;
            var dialog = new TemplateSelectionWindow(templates)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (dialog.ShowDialog() == true && dialog.SelectedTemplate != null)
            {
                if (_mainViewModel.SelectedTemplate2 != null &&
                    dialog.SelectedTemplate.ZipPath == _mainViewModel.SelectedTemplate2.ZipPath)
                {
                    MessageBox.Show("Dieses Design ist bereits im zweiten Slot ausgewählt!", "Doppelte Auswahl", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _mainViewModel.SelectedTemplate1 = dialog.SelectedTemplate;
                if (_mainViewModel.ActiveTemplate == null)
                    _mainViewModel.ActiveTemplate = dialog.SelectedTemplate;
                UpdateTemplateButtons();
            }
        }

        private void DeleteTemplateBtn1_Click(object sender, RoutedEventArgs e)
        {
            if (_mainViewModel.ActiveTemplate == _mainViewModel.SelectedTemplate1)
                _mainViewModel.ActiveTemplate = _mainViewModel.SelectedTemplate2;
            _mainViewModel.SelectedTemplate1 = null;
            UpdateTemplateButtons();
        }

        private void SelectTemplateBtn2_Click(object sender, RoutedEventArgs e)
        {
            var templates = _mainViewModel.Templates;
            var dialog = new TemplateSelectionWindow(templates)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (dialog.ShowDialog() == true && dialog.SelectedTemplate != null)
            {
                if (_mainViewModel.SelectedTemplate1 != null &&
                    dialog.SelectedTemplate.ZipPath == _mainViewModel.SelectedTemplate1.ZipPath)
                {
                    MessageBox.Show("Dieses Design ist bereits im ersten Slot ausgewählt!", "Doppelte Auswahl", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _mainViewModel.SelectedTemplate2 = dialog.SelectedTemplate;
                if (_mainViewModel.ActiveTemplate == null)
                    _mainViewModel.ActiveTemplate = dialog.SelectedTemplate;
                UpdateTemplateButtons();
            }
        }

        private void DeleteTemplateBtn2_Click(object sender, RoutedEventArgs e)
        {
            if (_mainViewModel.ActiveTemplate == _mainViewModel.SelectedTemplate2)
                _mainViewModel.ActiveTemplate = _mainViewModel.SelectedTemplate1;
            _mainViewModel.SelectedTemplate2 = null;
            UpdateTemplateButtons();
        }

        private void CameraSettings_Click(object sender, RoutedEventArgs e)
        {
            var vm = _mainViewModel;
            if (vm.AvailableCameras.Count == 0)
            {
                MessageBox.Show("Keine Kameras gefunden!", "Kamera-Auswahl", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new CameraSelectDialog(vm.AvailableCameras, vm.SelectedCameraName);
            dlg.Owner = Window.GetWindow(this);
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedCamera))
            {
                vm.SelectedCameraName = dlg.SelectedCamera;
            }
        }

        private void PrinterSettings_Click(object sender, RoutedEventArgs e)
        {
            var vm = _mainViewModel;
            if (vm.AvailablePrinters.Count == 0)
            {
                MessageBox.Show("Keine Drucker gefunden!", "Drucker-Auswahl", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new PrinterSelectDialog(vm.AvailablePrinters, vm.SelectedPrinterName);
            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedPrinter))
            {
                vm.SelectedPrinterName = dlg.SelectedPrinter;
            }
        }

        private void ShutdownSystem_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Windows wird heruntergefahren. Fortfahren?",
                "Fotobox ausschalten",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            if (TryShutdownWindows())
                Application.Current.Shutdown();
        }

        private void UpdateTemplateButtons()
        {
            var selected1 = _mainViewModel.SelectedTemplate1;
            if (selected1 != null && selected1.PreviewImage != null)
            {
                SelectedTemplatePreview1.Source = selected1.PreviewImage;
                SelectedTemplatePreview1.Visibility = Visibility.Visible;
                PlusIcon1.Visibility = Visibility.Collapsed;
                DeleteTemplateBtn1.Visibility = Visibility.Visible;
            }
            else
            {
                SelectedTemplatePreview1.Source = null;
                SelectedTemplatePreview1.Visibility = Visibility.Collapsed;
                PlusIcon1.Visibility = Visibility.Visible;
                DeleteTemplateBtn1.Visibility = Visibility.Collapsed;
            }

            var selected2 = _mainViewModel.SelectedTemplate2;
            if (selected2 != null && selected2.PreviewImage != null)
            {
                SelectedTemplatePreview2.Source = selected2.PreviewImage;
                SelectedTemplatePreview2.Visibility = Visibility.Visible;
                PlusIcon2.Visibility = Visibility.Collapsed;
                DeleteTemplateBtn2.Visibility = Visibility.Visible;
            }
            else
            {
                SelectedTemplatePreview2.Source = null;
                SelectedTemplatePreview2.Visibility = Visibility.Collapsed;
                PlusIcon2.Visibility = Visibility.Visible;
                DeleteTemplateBtn2.Visibility = Visibility.Collapsed;
            }
        }

        private static bool TryShutdownWindows()
        {
            try
            {
                var psi = new ProcessStartInfo("shutdown", "/s /t 0")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"System-Shutdown fehlgeschlagen:\n{ex.Message}", "Fotobox ausschalten",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
