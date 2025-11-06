using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
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

            _mainViewModel.Direktdruck = _mainViewModel.AllowDirektdruck;
            _mainViewModel.GalerieButton = _mainViewModel.AllowGalerie;
            _mainViewModel.FotoFilter = _mainViewModel.AllowFotoFilter;

            _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
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
            var templates = _mainViewModel.GetSelectableTemplates();
            if (templates.Count == 0)
            {
                MessageBox.Show("Es sind keine Designs freigegeben. Bitte im Admin-Menü Designs auswählen.", "Design-Auswahl", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
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

        private void ToggleCameraRotationUser_Click(object sender, RoutedEventArgs e)
        {
            _mainViewModel.CameraRotate180 = !_mainViewModel.CameraRotate180;
        }

        private void SelectTemplateBtn2_Click(object sender, RoutedEventArgs e)
        {
            if (!_mainViewModel.AllowTwoTemplates)
            {
                MessageBox.Show("Im Admin-Menü ist nur ein Foto-Design freigeschaltet.", "Zweiter Slot deaktiviert",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var templates = _mainViewModel.GetSelectableTemplates();
            if (templates.Count == 0)
            {
                MessageBox.Show("Es sind keine Designs freigegeben. Bitte im Admin-Menü Designs auswählen.", "Design-Auswahl", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
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

        private void UploadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Design hinzufügen",
                Filter = "Design-Pakete (*.zip)|*.zip",
                Multiselect = true,
                CheckFileExists = true
            };

            try
            {
                var templatesRoot = StartViewModel.GetTemplatesRootPath();
                if (!string.IsNullOrWhiteSpace(templatesRoot) && Directory.Exists(templatesRoot))
                {
                    dialog.InitialDirectory = templatesRoot;
                }
            }
            catch
            {
                // Initial directory fallback is best-effort.
            }

            var owner = Window.GetWindow(this);
            if (dialog.ShowDialog(owner) != true)
            {
                return;
            }

            var result = _mainViewModel.ImportTemplatesFromFiles(dialog.FileNames);

            AssignImportedTemplateToEmptySlot(result);
            UpdateTemplateButtons();
            ShowTemplateImportFeedback(result);
        }

        private void AssignImportedTemplateToEmptySlot(StartViewModel.TemplateImportResult result)
        {
            if (result == null || result.ImportedTemplates.Count == 0)
            {
                return;
            }

            var importedLookup = new HashSet<string>(result.ImportedTemplates, StringComparer.Ordinal);
            var importedTemplate = _mainViewModel.Templates
                .FirstOrDefault(t => importedLookup.Contains(t.Name));

            if (importedTemplate == null)
            {
                return;
            }

            if (_mainViewModel.SelectedTemplate1 == null)
            {
                _mainViewModel.SelectedTemplate1 = importedTemplate;
                if (_mainViewModel.ActiveTemplate == null)
                {
                    _mainViewModel.ActiveTemplate = importedTemplate;
                }
                return;
            }

            var alreadyUsed =
                string.Equals(_mainViewModel.SelectedTemplate1?.ZipPath, importedTemplate.ZipPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_mainViewModel.SelectedTemplate2?.ZipPath, importedTemplate.ZipPath, StringComparison.OrdinalIgnoreCase);

            if (alreadyUsed || !_mainViewModel.AllowTwoTemplates)
            {
                return;
            }

            if (_mainViewModel.SelectedTemplate2 == null)
            {
                _mainViewModel.SelectedTemplate2 = importedTemplate;
                if (_mainViewModel.ActiveTemplate == null)
                {
                    _mainViewModel.ActiveTemplate = importedTemplate;
                }
            }
        }

        private static void ShowTemplateImportFeedback(StartViewModel.TemplateImportResult result)
        {
            if (result == null)
            {
                return;
            }

            var messages = new List<string>();

            if (result.ImportedTemplates.Count > 0)
            {
                messages.Add(result.ImportedTemplates.Count == 1
                    ? $"1 neues Design importiert: {result.ImportedTemplates[0]}"
                    : $"{result.ImportedTemplates.Count} neue Designs importiert.");
            }

            if (result.UpdatedTemplates.Count > 0)
            {
                messages.Add(result.UpdatedTemplates.Count == 1
                    ? $"1 vorhandenes Design aktualisiert: {result.UpdatedTemplates[0]}"
                    : $"{result.UpdatedTemplates.Count} vorhandene Designs aktualisiert.");
            }

            if (result.InvalidFiles.Count > 0)
            {
                var invalidNames = result.InvalidFiles
                    .Select(Path.GetFileName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                if (invalidNames.Count > 0)
                {
                    const int maxListEntries = 3;
                    var displayNames = invalidNames.Take(maxListEntries).ToList();
                    var remainder = invalidNames.Count - displayNames.Count;
                    var summary = string.Join(", ", displayNames);
                    if (remainder > 0)
                    {
                        summary += $" +{remainder}";
                    }

                    messages.Add(result.InvalidFiles.Count == 1
                        ? $"1 Datei übersprungen (kein ZIP): {summary}"
                        : $"{result.InvalidFiles.Count} Dateien übersprungen (kein ZIP): {summary}");
                }
                else
                {
                    messages.Add(result.InvalidFiles.Count == 1
                        ? "1 Datei übersprungen (kein ZIP)."
                        : $"{result.InvalidFiles.Count} Dateien übersprungen (kein ZIP).");
                }
            }

            if (result.FailedFiles.Count > 0)
            {
                var failedNames = result.FailedFiles
                    .Select(f => Path.GetFileName(f.File))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                if (failedNames.Count > 0)
                {
                    const int maxListEntries = 3;
                    var displayNames = failedNames.Take(maxListEntries).ToList();
                    var remainder = failedNames.Count - displayNames.Count;
                    var summary = string.Join(", ", displayNames);
                    if (remainder > 0)
                    {
                        summary += $" +{remainder}";
                    }

                    messages.Add(result.FailedFiles.Count == 1
                        ? $"1 Datei konnte nicht übernommen werden: {summary}"
                        : $"{result.FailedFiles.Count} Dateien konnten nicht übernommen werden: {summary}");
                }
                else
                {
                    messages.Add(result.FailedFiles.Count == 1
                        ? "1 Datei konnte nicht übernommen werden."
                        : $"{result.FailedFiles.Count} Dateien konnten nicht übernommen werden.");
                }
            }

            if (messages.Count == 0)
            {
                messages.Add("Es wurden keine gültigen Designs ausgewählt.");
            }

            var icon = result.FailedFiles.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
            MessageBox.Show(string.Join(Environment.NewLine, messages), "Design-Upload", MessageBoxButton.OK, icon);
        }

        private void CameraSettings_Click(object sender, RoutedEventArgs e)
        {
            var vm = _mainViewModel;
            var selectable = vm.GetSelectableCameras();

            if (selectable.Count == 0)
            {
                MessageBox.Show("Keine Kameras gefunden!", "Kamera-Auswahl", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new CameraSelectDialog(selectable, vm.SelectedCameraName);
            dlg.Owner = Window.GetWindow(this);
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedCamera))
            {
                vm.SelectedCameraName = dlg.SelectedCamera;
            }
        }

        private void UsbSettings_Click(object sender, RoutedEventArgs e)
        {
            var vm = _mainViewModel;
            vm.RefreshUsbDrives();

            if (vm.AvailableUsbDrives.Count == 0)
            {
                MessageBox.Show("Kein USB-Speicher gefunden!", "USB-Auswahl", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new CameraSelectDialog(vm.AvailableUsbDrives, vm.SelectedUsbDrivePath)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedCamera))
            {
                vm.SelectedUsbDrivePath = dlg.SelectedCamera;
            }
        }

        private void PrinterSettings_Click(object sender, RoutedEventArgs e)
        {
            var vm = _mainViewModel;
            var selectable = vm.GetSelectablePrinters();

            if (selectable.Count == 0)
            {
                MessageBox.Show("Keine Drucker gefunden!", "Drucker-Auswahl", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new PrinterSelectDialog(selectable, vm.SelectedPrinterName);
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

        private void MainViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(StartViewModel.AllowTwoTemplates):
                case nameof(StartViewModel.SelectedTemplate1):
                case nameof(StartViewModel.SelectedTemplate2):
                case nameof(StartViewModel.ActiveTemplate):
                    Dispatcher.Invoke(UpdateTemplateButtons);
                    break;
            }
        }

        private void UpdateTemplateButtons()
        {
            TemplateSlot2.Visibility = _mainViewModel.AllowTwoTemplates ? Visibility.Visible : Visibility.Collapsed;
            SelectTemplateBtn2.IsEnabled = _mainViewModel.AllowTwoTemplates;
            DeleteTemplateBtn2.IsEnabled = _mainViewModel.AllowTwoTemplates;

            var selected1 = _mainViewModel.SelectedTemplate1;
            if (selected1 != null && selected1.PreviewImage != null)
            {
                SelectedTemplatePreview1.Source = selected1.PreviewImage;
                SelectedTemplatePreview1.Visibility = Visibility.Visible;
                PlusIcon1.Visibility = Visibility.Collapsed;
                DeleteTemplateBtn1.Visibility = Visibility.Visible;
                DeleteTemplateBtn1.IsEnabled = true;
                DeleteTemplateBtn1.Opacity = 1.0;
            }
            else
            {
                SelectedTemplatePreview1.Source = null;
                SelectedTemplatePreview1.Visibility = Visibility.Collapsed;
                PlusIcon1.Visibility = Visibility.Visible;
                DeleteTemplateBtn1.Visibility = Visibility.Visible;
                DeleteTemplateBtn1.IsEnabled = false;
                DeleteTemplateBtn1.Opacity = 0.4;
            }

            var selected2 = _mainViewModel.SelectedTemplate2;
            if (selected2 != null && selected2.PreviewImage != null)
            {
                SelectedTemplatePreview2.Source = selected2.PreviewImage;
                SelectedTemplatePreview2.Visibility = Visibility.Visible;
                PlusIcon2.Visibility = Visibility.Collapsed;
                DeleteTemplateBtn2.Visibility = Visibility.Visible;
                DeleteTemplateBtn2.IsEnabled = true;
                DeleteTemplateBtn2.Opacity = 1.0;
            }
            else
            {
                SelectedTemplatePreview2.Source = null;
                SelectedTemplatePreview2.Visibility = Visibility.Collapsed;
                PlusIcon2.Visibility = _mainViewModel.AllowTwoTemplates ? Visibility.Visible : Visibility.Collapsed;
                DeleteTemplateBtn2.Visibility = Visibility.Visible;
                DeleteTemplateBtn2.IsEnabled = false;
                DeleteTemplateBtn2.Opacity = 0.4;
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


