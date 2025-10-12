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

        private void SetGalleryName_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new GalerieNameDialog(_vm.GalleryName)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() == true)
            {
                _vm.GalleryName = dialog.GalleryName;
            }
        }

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
            var pinDialog = new PinPadDialog
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            bool? result = pinDialog.ShowDialog();

            if (result == true)
            {
                var window = Window.GetWindow(this) as MainWindow;
                if (window == null)
                {
                    MessageBox.Show("MainWindow nicht gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (pinDialog.EnteredPin == "1111")
                    window.MainFrame.Navigate(new UserMenuView(window.MainViewModel));
                else if (pinDialog.EnteredPin == "0410")
                    window.MainFrame.Navigate(new AdminMenuView(window.MainViewModel));
                else
                    MessageBox.Show("Falscher Code!", "Zugriff verweigert", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
