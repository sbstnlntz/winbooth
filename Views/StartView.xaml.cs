using System;
using System.Windows;
using System.Windows.Controls;
using FotoboxApp.ViewModels;
using FotoboxApp.Utilities;
using FotoboxApp.Models;


namespace FotoboxApp.Views
{
    public partial class StartView : UserControl
    {
        private readonly StartViewModel _vm;
        private string _enteredPin = string.Empty;

        public StartView(StartViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;
        }

        public StartView() : this(new StartViewModel()) { }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Galerie-Name wird nur im Admin-Menü geändert (Start-Menü zeigt/ändert ihn nicht mehr)

        private void ResetTotalPhotoCounter_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Möchtest du wirklich den Gesamtzähler auf 0 setzen?", "Zähler zurücksetzen", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                StatManager.ResetTotalPhotoCount();
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            // Sicherstellen, dass ein Template gewählt wurde
            if (_vm.ActiveTemplate == null)
            {
                MessageBox.Show("Bitte wähle ein Foto-Design unten rechts!", "Vorlage fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Sicherstellen, dass ein Galerie-Name eingegeben wurde
            if (string.IsNullOrWhiteSpace(_vm.GalleryName))
            {
                MessageBox.Show("Bitte gib einen Galerie-Namen ein!", "Name fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Zur CameraView navigieren, ViewModel mitgeben
            var window = Window.GetWindow(this) as MainWindow;
            if (window != null)
            {
                window.MainFrame.Navigate(
                    new CameraView(
                        _vm.ActiveTemplate.ZipPath,
                        _vm.GalleryName,
                        _vm   // Das ViewModel wird mitgegeben!
                    )
                );
            }
        }



        private void BtnGallery_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this) as MainWindow;
            window?.MainFrame.Navigate(
                new LiveGalerieView(
                    _vm.ActiveTemplate?.ZipPath ?? "",
                    _vm.GalleryName ?? ""
                )
            );
        }



        private void SelectTemplate1_Click(object sender, RoutedEventArgs e)
        {
            _vm.ActiveTemplate = _vm.SelectedTemplate1;
        }
        private void SelectTemplate2_Click(object sender, RoutedEventArgs e)
        {
            _vm.ActiveTemplate = _vm.SelectedTemplate2;
        }


        private void BtnAdmin_Click(object sender, RoutedEventArgs e)
        {
            _enteredPin = string.Empty;
            PinDisplay.Text = string.Empty;
            PinOverlay.Visibility = Visibility.Visible;
        }

        private void PinNumber_Click(object sender, RoutedEventArgs e)
        {
            if (_enteredPin.Length >= 4) return;
            if (sender is Button btn && btn.Content != null)
            {
                var s = btn.Content.ToString();
                if (!string.IsNullOrEmpty(s) && char.IsDigit(s[0]))
                {
                    _enteredPin += s[0];
                    PinDisplay.Text = new string('*', _enteredPin.Length);
                    if (_enteredPin.Length == 4)
                    {
                        // Automatisch bestätigen, kein zusätzlicher OK-Klick nötig
                        PinOk_Click(null, null);
                    }
                }
            }
        }

        private void PinBack_Click(object sender, RoutedEventArgs e)
        {
            if (_enteredPin.Length > 0)
            {
                _enteredPin = _enteredPin.Substring(0, _enteredPin.Length - 1);
                PinDisplay.Text = new string('*', _enteredPin.Length);
            }
        }

        private void PinCancel_Click(object sender, RoutedEventArgs e)
        {
            _enteredPin = string.Empty;
            PinDisplay.Text = string.Empty;
            PinOverlay.Visibility = Visibility.Collapsed;
        }

        private void PinOk_Click(object sender, RoutedEventArgs e)
        {
            if (_enteredPin.Length != 4)
            {
                MessageBox.Show("Bitte 4 Ziffern eingeben!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = Window.GetWindow(this) as MainWindow;
            if (window == null)
            {
                MessageBox.Show("MainWindow nicht gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var userPin = System.Environment.GetEnvironmentVariable("FOTOBOX_USER_PIN") ?? "1111";
            var adminPin = System.Environment.GetEnvironmentVariable("FOTOBOX_ADMIN_PIN") ?? "0410";

            if (_enteredPin == userPin)
            {
                PinOverlay.Visibility = Visibility.Collapsed;
                window.MainFrame.Navigate(new UserMenuView(window.MainViewModel));
            }
            else if (_enteredPin == adminPin)
            {
                PinOverlay.Visibility = Visibility.Collapsed;
                window.MainFrame.Navigate(new AdminMenuView(window.MainViewModel));
            }
            else
            {
                MessageBox.Show("Falscher Code!", "Zugriff verweigert", MessageBoxButton.OK, MessageBoxImage.Error);
                _enteredPin = string.Empty;
                PinDisplay.Text = string.Empty;
            }
        }

    }
}


