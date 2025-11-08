using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using winbooth.Services;
using winbooth.ViewModels;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace winbooth.Views
{
    public partial class AdminMenuView : UserControl
    {
        private const int MaxEventCodeLength = 12;

        public AdminMenuView()
        {
            InitializeComponent();
        }

        public AdminMenuView(StartViewModel vm) : this()
        {
            DataContext = vm;
            vm?.RefreshTemplatesFromDisk();
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
            NewGalleryNameBox.Text = string.Empty;
            NewEventOverlay.Visibility = Visibility.Visible;
        }

        private void NewEventDigit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: not null } button)
                return;

            var digit = button.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(digit) || digit.Any(c => !char.IsDigit(c)))
                return;

            var appended = (NewGalleryNameBox.Text ?? string.Empty) + digit;
            NewGalleryNameBox.Text = NormalizeEventCode(appended);
        }

        private void NewEventBackspace_Click(object sender, RoutedEventArgs e)
        {
            var current = NewGalleryNameBox.Text ?? string.Empty;
            if (current.Length == 0)
                return;

            NewGalleryNameBox.Text = current.Substring(0, current.Length - 1);
        }

        private string NormalizeEventCode(string rawValue)
        {
            if (string.IsNullOrEmpty(rawValue))
                return string.Empty;

            var digitsOnly = new string(rawValue.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length > MaxEventCodeLength)
                return digitsOnly.Substring(0, MaxEventCodeLength);

            return digitsOnly;
        }

        private void OpenArchive_Click(object sender, RoutedEventArgs e)
        {
            RefreshBackupsList();
            BackupsOverlay.Visibility = Visibility.Visible;
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

        private void OpenDelaySettings_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            var window = new DelaySettingsWindow(vm)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            window.ShowDialog();
        }

        private void OpenDefaultTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            var window = new DefaultTemplateWindow(vm)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            window.ShowDialog();
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

        private void OpenDesignSettings_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            var window = new DesignManagementWindow(vm)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            window.ShowDialog();
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
            string newName = NormalizeEventCode(NewGalleryNameBox.Text ?? string.Empty);
            NewGalleryNameBox.Text = newName;

            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Bitte eine gültige Eventnummer eingeben.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var picturesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Fotobox");
            var newGalleryDir = Path.Combine(picturesRoot, newName);

            var galleryAlreadyExists = Directory.Exists(newGalleryDir) &&
                                       !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase);
            var backupAlreadyExists = BackupService.BackupExists(newName);

            if (galleryAlreadyExists || backupAlreadyExists)
            {
                var conflicts = new List<string>();
                if (galleryAlreadyExists)
                    conflicts.Add("ein Galerie-Ordner");
                if (backupAlreadyExists)
                    conflicts.Add("mindestens ein Backup");

                var conflictText = string.Join(" und ", conflicts);
                var duplicateResult = MessageBox.Show(
                    $"Es existiert bereits {conflictText} mit diesem Namen.\nTrotzdem neues Event anlegen?",
                    "Event bereits vorhanden",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (duplicateResult != MessageBoxResult.Yes)
                    return;
            }

            if (!string.IsNullOrWhiteSpace(oldName))
            {
                var oldDir = Path.Combine(picturesRoot, oldName);
                if (Directory.Exists(oldDir))
                {
                    try
                    {
                        BackupService.CreateBackup(oldName);
                        BackupService.DeleteGalleryDirectory(oldName);

                        if (BackupsOverlay.Visibility == Visibility.Visible)
                            RefreshBackupsList();
                    }
                    catch (Exception ex)
                    {
                        var backupFailed = MessageBox.Show(
                            $"Das vorherige Event konnte nicht gesichert:\n{ex.Message}\nNeues Event ohne Backup anlegen?",
                            "Backup fehlgeschlagen",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (backupFailed != MessageBoxResult.Yes)
                            return;
                    }
                }
            }

            vm.GalleryName = newName;

            try
            {
                Directory.CreateDirectory(newGalleryDir);
            }
            catch
            {
                // Ignorieren, Ordner wird später angelegt.
            }

            vm.ResetEventScopedState();

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

        private bool TryGetSelectedBackup(out FileInfo selected)
        {
            if (BackupsList.SelectedItem is FileInfo file)
            {
                selected = file;
                return true;
            }

            MessageBox.Show("Bitte zuerst ein Backup auswählen.", "Backup",
                MessageBoxButton.OK, MessageBoxImage.Information);
            selected = null;
            return false;
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedBackup(out var selected))
                return;

            if (DataContext is not StartViewModel vm)
                return;

            var galleryName = vm.GalleryName?.Trim();
            if (string.IsNullOrWhiteSpace(galleryName))
            {
                MessageBox.Show("Kein Galerie-Name gesetzt. Bitte zuerst festlegen.", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var defaultRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Fotobox");

            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = $"Bitte den Speicherort für die Galerie \"{galleryName}\" auswählen.",
                ShowNewFolderButton = true
            };

            if (Directory.Exists(defaultRoot))
                dialog.SelectedPath = defaultRoot;

            var dialogResult = dialog.ShowDialog();
            if (dialogResult != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                return;

            var selectedPath = Path.GetFullPath(dialog.SelectedPath.Trim());
            var selectedLeaf = new DirectoryInfo(selectedPath).Name;
            var targetDir = string.Equals(selectedLeaf, galleryName, StringComparison.OrdinalIgnoreCase)
                ? selectedPath
                : Path.Combine(selectedPath, galleryName);

            if (Directory.Exists(targetDir) && Directory.EnumerateFileSystemEntries(targetDir).Any())
            {
                var confirm = MessageBox.Show(
                    $"Der Zielordner \"{targetDir}\" enthält bereits Dateien. Soll er überschrieben werden?",
                    "Backup wiederherstellen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            try
            {
                BackupService.RestoreBackup(selected.FullName, targetDir);
                MessageBox.Show("Backup wurde wiederhergestellt.", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wiederherstellung fehlgeschlagen:\n{ex.Message}", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CopyBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedBackup(out var selected))
                return;

            var dialog = new SaveFileDialog
            {
                Title = "Backup kopieren",
                Filter = "ZIP-Archive (*.zip)|*.zip|Alle Dateien (*.*)|*.*",
                FileName = selected.Name,
                OverwritePrompt = true
            };

            if (DataContext is StartViewModel vm)
            {
                var usbPath = vm.SelectedUsbDrivePath;
                if (!string.IsNullOrWhiteSpace(usbPath) && Directory.Exists(usbPath))
                {
                    dialog.InitialDirectory = usbPath;
                }
            }

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                File.Copy(selected.FullName, dialog.FileName, overwrite: true);
                MessageBox.Show("Backup wurde kopiert.", "Backup kopieren",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup konnte nicht kopiert werden:\n{ex.Message}", "Backup kopieren",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedBackup(out var selected))
                return;

            var confirm = MessageBox.Show(
                $"Backup \"{selected.Name}\" wirklich löschen?",
                "Backup löschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                BackupService.DeleteBackupFile(selected.FullName);
                RefreshBackupsList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup konnte nicht gelöscht werden:\n{ex.Message}", "Backup löschen",
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
