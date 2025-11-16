// Connects the user menu view with the main view model and manages command handlers, file pickers, and overlay toggles.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using Microsoft.Win32;
using winbooth.ViewModels;

namespace winbooth.Views
{
    public partial class UserMenuView : UserControl
    {
        private readonly StartViewModel _mainViewModel;

        public UserMenuView(StartViewModel mainViewModel)
        {
            InitializeComponent();
            _mainViewModel = mainViewModel;
            DataContext = mainViewModel; // Important: reuse the shared StartViewModel instance.

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

        private async void SelectTemplateBtn1_Click(object sender, RoutedEventArgs e)
            => await HandleTemplateSelectionAsync(slotIndex: 1);

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

        private async void SelectTemplateBtn2_Click(object sender, RoutedEventArgs e)
            => await HandleTemplateSelectionAsync(slotIndex: 2);

        private void DeleteTemplateBtn2_Click(object sender, RoutedEventArgs e)
        {
            if (_mainViewModel.ActiveTemplate == _mainViewModel.SelectedTemplate2)
                _mainViewModel.ActiveTemplate = _mainViewModel.SelectedTemplate1;
            _mainViewModel.SelectedTemplate2 = null;
            UpdateTemplateButtons();
        }

        private async Task HandleTemplateSelectionAsync(int slotIndex)
        {
            if (slotIndex == 2 && !_mainViewModel.AllowTwoTemplates)
            {
                MessageBox.Show("Im Admin-Menü ist nur ein Foto-Design freigeschaltet.", "Zweiter Slot deaktiviert",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TemplateSourceChoice choice;
            try
            {
                var owner = Window.GetWindow(this);
                var sourceDialog = new TemplateSourceChoiceWindow
                {
                    Owner = owner,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                var result = sourceDialog.ShowDialog();
                if (result != true || sourceDialog.Choice == TemplateSourceChoice.None)
                    return;

                choice = sourceDialog.Choice;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Die Design-Auswahl konnte nicht geöffnet werden:\n{ex.Message}",
                    "Design-Auswahl", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (choice == TemplateSourceChoice.Usb)
            {
                await SelectTemplateFromUsbAsync(slotIndex);
            }
            else if (choice == TemplateSourceChoice.Standard)
            {
                await SelectTemplateFromStandardLibraryAsync(slotIndex);
            }
        }

        private async Task SelectTemplateFromStandardLibraryAsync(int slotIndex)
        {
            var templates = _mainViewModel.GetTemplateLibraryTemplates();
            if (templates == null || templates.Count == 0)
            {
                MessageBox.Show("Es wurden keine Standard-Designs gefunden. Bitte im Admin-Menü unter \"Standard-Design\" ein Design hinzufügen.",
                    "Design-Bibliothek", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new TemplateSelectionWindow(_mainViewModel)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() == true && dialog.SelectedTemplate != null)
            {
                await ImportTemplateFromLibraryAsync(dialog.SelectedTemplate.ZipPath, slotIndex);
            }
        }

        private async Task SelectTemplateFromUsbAsync(int slotIndex)
        {
            var usbPath = _mainViewModel.SelectedUsbDrivePath;
            if (string.IsNullOrWhiteSpace(usbPath) || !Directory.Exists(usbPath))
            {
                MessageBox.Show("Bitte zuerst rechts einen USB-Speicher auswählen. Es können nur Designs von diesem USB-Stick geladen werden.",
                    "USB erforderlich", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var owner = Window.GetWindow(this);
            var dialog = new OpenFileDialog
            {
                Filter = "Design-Pakete (*.zip)|*.zip",
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = usbPath,
                Title = "Eigenes Design vom USB-Stick auswählen"
            };

            if (dialog.ShowDialog(owner) != true)
                return;

            var selectedPath = Path.GetFullPath(dialog.FileName);
            var usbRoot = Path.GetFullPath(usbPath);
            if (!selectedPath.StartsWith(usbRoot, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Es dürfen ausschließlich Designs vom ausgewählten USB-Stick geladen werden.",
                    "Ungültiger Speicherort", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await ImportTemplateFromLibraryAsync(selectedPath, slotIndex);
        }

        private async Task ImportTemplateFromLibraryAsync(string sourceZipPath, int slotIndex)
        {
            var result = await _mainViewModel.ImportTemplatesFromFilesAsync(new[] { sourceZipPath });
            if (result == null || !result.HasChanges)
                return;

            await _mainViewModel.WaitForTemplateReloadAsync();

            var templateName = Path.GetFileNameWithoutExtension(sourceZipPath);
            var template = _mainViewModel.FindTemplateByName(templateName);

            if (template == null)
            {
                foreach (var name in result.ImportedTemplates.Concat(result.UpdatedTemplates))
                {
                    template = _mainViewModel.FindTemplateByName(name);
                    if (template != null)
                        break;
                }
            }

            if (template == null)
            {
                MessageBox.Show("Das Design konnte nicht übernommen werden.", "Design-Auswahl",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (slotIndex == 1 &&
                _mainViewModel.SelectedTemplate2 != null &&
                string.Equals(template.ZipPath, _mainViewModel.SelectedTemplate2.ZipPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Dieses Design ist bereits im zweiten Slot ausgewählt!", "Doppelte Auswahl",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (slotIndex == 2 &&
                _mainViewModel.SelectedTemplate1 != null &&
                string.Equals(template.ZipPath, _mainViewModel.SelectedTemplate1.ZipPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Dieses Design ist bereits im ersten Slot ausgewählt!", "Doppelte Auswahl",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (slotIndex == 1)
                _mainViewModel.SelectedTemplate1 = template;
            else
                _mainViewModel.SelectedTemplate2 = template;

            if (_mainViewModel.ActiveTemplate == null)
                _mainViewModel.ActiveTemplate = template;

            UpdateTemplateButtons();
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
                case nameof(StartViewModel.TemplateSlot1Template):
                case nameof(StartViewModel.TemplateSlot1Preview):
                case nameof(StartViewModel.TemplateSlot2Template):
                case nameof(StartViewModel.TemplateSlot2Preview):
                    Dispatcher.Invoke(UpdateTemplateButtons);
                    break;
            }
        }

        private void UpdateTemplateButtons()
        {
            TemplateSlot2.Visibility = _mainViewModel.AllowTwoTemplates ? Visibility.Visible : Visibility.Collapsed;
            SelectTemplateBtn2.IsEnabled = _mainViewModel.AllowTwoTemplates;
            DeleteTemplateBtn1.Visibility = Visibility.Visible;
            DeleteTemplateBtn2.Visibility = Visibility.Visible;

            var slot1Template = _mainViewModel.TemplateSlot1Template;
            var slot1Preview = _mainViewModel.TemplateSlot1Preview;
            if (slot1Template != null && slot1Preview != null)
            {
                SelectedTemplatePreview1.Source = slot1Preview;
                SelectedTemplatePreview1.Visibility = Visibility.Visible;
                PlusIcon1.Visibility = Visibility.Collapsed;
            }
            else
            {
                SelectedTemplatePreview1.Source = null;
                SelectedTemplatePreview1.Visibility = Visibility.Collapsed;
                PlusIcon1.Visibility = Visibility.Visible;
            }

            var selected2 = _mainViewModel.SelectedTemplate2;
            var slot2Preview = selected2?.PreviewImage;
            if (selected2 != null && slot2Preview != null)
            {
                SelectedTemplatePreview2.Source = slot2Preview;
                SelectedTemplatePreview2.Visibility = Visibility.Visible;
                PlusIcon2.Visibility = Visibility.Collapsed;
            }
            else
            {
                SelectedTemplatePreview2.Source = null;
                SelectedTemplatePreview2.Visibility = Visibility.Collapsed;
                PlusIcon2.Visibility = _mainViewModel.AllowTwoTemplates ? Visibility.Visible : Visibility.Collapsed;
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


