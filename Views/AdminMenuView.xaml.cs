using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FotoboxApp.Models;
using FotoboxApp.Services;
using FotoboxApp.ViewModels;

namespace FotoboxApp.Views
{
    public partial class AdminMenuView : UserControl
    {
        public ObservableCollection<TemplateOption> TemplateOptions { get; } = new();

        public sealed class TemplateOption : INotifyPropertyChanged
        {
            private bool _isAllowed;

            public TemplateOption(TemplateItem template, bool isAllowed)
            {
                Template = template;
                _isAllowed = isAllowed;
            }

            public TemplateItem Template { get; }

            public bool IsAllowed
            {
                get => _isAllowed;
                set
                {
                    if (_isAllowed == value)
                        return;

                    _isAllowed = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAllowed)));
                }
            }

            public string Name => Template?.Name ?? "Unbekanntes Design";

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public AdminMenuView()
        {
            InitializeComponent();
        }

        public AdminMenuView(StartViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm) return;
            if (Application.Current.MainWindow is MainWindow wnd)
            {
                wnd.MainFrame.Navigate(new StartView(vm));
            }
        }

        private void NewEvent_Click(object sender, RoutedEventArgs e)
        {
            NewGalleryNameBox.Text = (DataContext as StartViewModel)?.GalleryName ?? string.Empty;
            NewEventOverlay.Visibility = Visibility.Visible;
        }

        private void OpenArchive_Click(object sender, RoutedEventArgs e)
        {
            RefreshBackupsList();
            BackupsOverlay.Visibility = Visibility.Visible;
        }

        private void DeleteDesign_Click(object sender, RoutedEventArgs e)
        {
            ConfigureTemplates_Click(sender, e);
        }

        private void AddDesign_Click(object sender, RoutedEventArgs e)
        {
            ImportTemplatesViaDialog();
        }

        private void OpenGraphicsManager_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            if (Application.Current.MainWindow is MainWindow wnd)
            {
                wnd.MainFrame.Navigate(new AdminGraphicsView(vm));
            }
        }

        private void SyncTemplateOptions(StartViewModel vm)
        {
            if (vm == null)
                return;

            var previousSelectionName = (TemplatesList?.SelectedItem as TemplateOption)?.Template?.Name;

            var allowed = new HashSet<string>(
                vm.AllowedTemplateNames ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            TemplateOptions.Clear();
            foreach (var template in vm.Templates)
            {
                TemplateOptions.Add(new TemplateOption(template, allowed.Contains(template.Name)));
            }

            if (TemplatesList == null || TemplateOptions.Count == 0)
            {
                return;
            }

            TemplateOption nextSelection = null;
            if (!string.IsNullOrWhiteSpace(previousSelectionName))
            {
                nextSelection = TemplateOptions.FirstOrDefault(option =>
                    option.Template != null &&
                    string.Equals(option.Template.Name, previousSelectionName, StringComparison.Ordinal));
            }

            TemplatesList.SelectedItem = nextSelection ?? TemplateOptions.FirstOrDefault();
        }

        private void CameraSettingsAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            if (vm.AvailableCameras.Count == 0)
            {
                MessageBox.Show("Es wurden keine Kameras gefunden.", "Kamera-Auswahl",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new DeviceMultiSelectDialog(
                vm.AvailableCameras,
                vm.AllowedCameraNames,
                "Kameras freigeben")
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() == true)
            {
                vm.UpdateAllowedCameras(dialog.SelectedItems);
            }
        }

        private void UsbSettingsAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            vm.RefreshUsbDrives();

            if (vm.AvailableUsbDrives.Count == 0)
            {
                MessageBox.Show("Es wurden keine USB-Speicher erkannt.", "USB-Auswahl",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new CameraSelectDialog(vm.AvailableUsbDrives, vm.SelectedUsbDrivePath)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedCamera))
            {
                vm.SelectedUsbDrivePath = dialog.SelectedCamera;
            }
        }

        private void PrinterSettingsAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            if (vm.AvailablePrinters.Count == 0)
            {
                MessageBox.Show("Es wurden keine Drucker gefunden.", "Drucker-Auswahl",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new DeviceMultiSelectDialog(
                vm.AvailablePrinters,
                vm.AllowedPrinterNames,
                "Drucker freigeben")
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() == true)
            {
                vm.UpdateAllowedPrinters(dialog.SelectedItems);
            }
        }

        private void ConfigureTemplates_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            vm.RefreshTemplatesFromDisk();
            SyncTemplateOptions(vm);
            if (NewEventOverlay != null)
                NewEventOverlay.Visibility = Visibility.Collapsed;
            if (BackupsOverlay != null)
                BackupsOverlay.Visibility = Visibility.Collapsed;
            TemplatesOverlay.Visibility = Visibility.Visible;
            if (TemplatesList != null && TemplatesList.Items.Count > 0)
            {
                TemplatesList.SelectedIndex = 0;
            }
        }

        private void AddTemplatesOverlay_Click(object sender, RoutedEventArgs e)
        {
            ImportTemplatesViaDialog();
        }

        private void DeleteSelectedTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (TemplatesList.SelectedItem is not TemplateOption option || option.Template == null)
                return;

            var template = option.Template;

            var confirm = MessageBox.Show(
                $"Soll das Design \"{template.Name}\" dauerhaft gelöscht werden?",
                "Designs verwalten",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            if (DataContext is not StartViewModel vm)
                return;

            if (!vm.TryDeleteTemplate(template, out var errorMessage))
            {
                MessageBox.Show(errorMessage ?? "Löschen fehlgeschlagen.", "Designs verwalten",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"Design \"{template.Name}\" wurde entfernt.", "Designs verwalten",
                MessageBoxButton.OK, MessageBoxImage.Information);

            SyncTemplateOptions(vm);

            if (TemplatesList != null && TemplatesList.Items.Count > 0)
            {
                TemplatesList.SelectedIndex = 0;
            }
        }

        private void SaveTemplatesSelection_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            var selectedNames = TemplateOptions
                .Where(o => o.IsAllowed && !string.IsNullOrWhiteSpace(o.Template?.Name))
                .Select(o => o.Template.Name)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (selectedNames.Count == 0)
            {
                MessageBox.Show("Bitte mindestens ein Design auswählen.", "Designs verwalten",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            vm.UpdateAllowedTemplates(selectedNames);
            SyncTemplateOptions(vm);
            TemplatesOverlay.Visibility = Visibility.Collapsed;
        }

        private void CloseTemplatesOverlay_Click(object sender, RoutedEventArgs e)
        {
            TemplatesOverlay.Visibility = Visibility.Collapsed;
        }

        private void ImportTemplatesViaDialog()
        {
            if (DataContext is not StartViewModel vm)
                return;

            var fileDialog = new OpenFileDialog
            {
                Title = "Designs hinzufügen",
                Filter = "Design-Pakete (*.zip)|*.zip",
                Multiselect = true,
                CheckFileExists = true
            };

            try
            {
                var templatesRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Fotobox",
                    "templates");

                if (Directory.Exists(templatesRoot))
                {
                    fileDialog.InitialDirectory = templatesRoot;
                }
            }
            catch { }

            var owner = Window.GetWindow(this);
            if (fileDialog.ShowDialog(owner) != true)
            {
                return;
            }

            var result = vm.ImportTemplatesFromFiles(fileDialog.FileNames);
            SyncTemplateOptions(vm);
            ShowTemplateImportResult(result);

            if (TemplatesOverlay.Visibility == Visibility.Visible &&
                TemplatesList != null &&
                TemplatesList.Items.Count > 0)
            {
                TemplatesList.SelectedIndex = 0;
            }
        }

        private static void ShowTemplateImportResult(StartViewModel.TemplateImportResult result)
        {
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
            MessageBox.Show(string.Join(Environment.NewLine, messages), "Designs verwalten", MessageBoxButton.OK, icon);
        }

        private void ToggleCameraRotation_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            vm.CameraRotate180 = !vm.CameraRotate180;
        }

        private void BackupNow_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm) return;

            var galleryName = vm.GalleryName?.Trim();
            if (string.IsNullOrWhiteSpace(galleryName))
            {
                MessageBox.Show("Bitte zuerst einen Galerie-Namen festlegen.", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var galleryDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Fotobox",
                galleryName);

            if (!Directory.Exists(galleryDir))
            {
                MessageBox.Show("Kein Galerie-Ordner gefunden, es gibt nichts zu sichern.", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                BackupService.CreateBackup(galleryName);
                MessageBox.Show("Backup wurde erstellt.", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                if (BackupsOverlay.Visibility == Visibility.Visible)
                    RefreshBackupsList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup fehlgeschlagen:\n{ex.Message}", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void NewEventCancel_Click(object sender, RoutedEventArgs e)
        {
            NewEventOverlay.Visibility = Visibility.Collapsed;
        }

        private void NewEventConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
            {
                NewEventOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            string oldName = vm.GalleryName ?? string.Empty;
            string newName = (NewGalleryNameBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Bitte einen gültigen Galerie-Namen eingeben.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var oldDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Fotobox",
                    oldName);

                if (!string.IsNullOrWhiteSpace(oldName) && Directory.Exists(oldDir))
                {
                    BackupService.CreateBackup(oldName);
                    if (BackupsOverlay.Visibility == Visibility.Visible)
                        RefreshBackupsList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup fehlgeschlagen:\n{ex.Message}", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            vm.GalleryName = newName;

            try
            {
                var newDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Fotobox",
                    newName);
                Directory.CreateDirectory(newDir);
            }
            catch
            {
                // Ignorieren, Ordner wird später angelegt.
            }

            NewEventOverlay.Visibility = Visibility.Collapsed;
            MessageBox.Show("Neues Event angelegt.", "Admin",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseBackups_Click(object sender, RoutedEventArgs e)
        {
            BackupsOverlay.Visibility = Visibility.Collapsed;
        }

        private void OpenBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = BackupService.GetBackupRoot();
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch { }
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (BackupsList.SelectedItem is not FileInfo selected)
            {
                MessageBox.Show("Bitte zuerst ein Backup auswählen.", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (DataContext is not StartViewModel vm)
                return;

            var galleryName = vm.GalleryName?.Trim();
            if (string.IsNullOrWhiteSpace(galleryName))
            {
                MessageBox.Show("Kein Galerie-Name gesetzt. Bitte zuerst festlegen.", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var targetDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Fotobox",
                galleryName);

            if (Directory.Exists(targetDir) && Directory.EnumerateFileSystemEntries(targetDir).Any())
            {
                var confirm = MessageBox.Show(
                    "Der aktuelle Galerie-Ordner enthält bereits Dateien. Soll er überschrieben werden?",
                    "Backup wiederherstellen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            try
            {
                BackupService.RestoreBackup(selected.FullName, galleryName);
                MessageBox.Show("Backup wurde wiederhergestellt.", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wiederherstellung fehlgeschlagen:\n{ex.Message}", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShutdownApp_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Soll die Fotobox-App beendet werden?",
                "App beenden",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
                Application.Current.Shutdown();
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

        private void RefreshBackupsList()
        {
            BackupsList.ItemsSource = BackupService.ListBackups()
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();
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
