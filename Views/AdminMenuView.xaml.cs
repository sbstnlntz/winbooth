using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FotoboxApp.Services;
using FotoboxApp.ViewModels;

namespace FotoboxApp.Views
{
    public partial class AdminMenuView : UserControl
    {
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
            MessageBox.Show("Design entfernt.", "Admin", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddDesign_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Neues Design hinzugefügt.", "Admin", MessageBoxButton.OK, MessageBoxImage.Information);
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
