using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using winbooth.ViewModels;

namespace winbooth.Views
{
    public partial class AdminGraphicsView : UserControl
    {
        public AdminGraphicsView()
        {
            InitializeComponent();
        }

        public AdminGraphicsView(StartViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            if (Application.Current.MainWindow is MainWindow wnd)
            {
                wnd.MainFrame.Navigate(new AdminMenuView(vm));
            }
        }

        private static string SelectGraphicFile(string title)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Bilder (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Alle Dateien (*.*)|*.*",
                Title = title
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private void ChangeStartInstruction_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            var file = SelectGraphicFile("Grafik für \"So funktioniert die Fotobox\" auswählen");
            if (string.IsNullOrEmpty(file))
                return;

            if (!vm.TryUpdateStartInstructionGraphic(file, out var error))
            {
                MessageBox.Show(error, "Grafik ändern", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Die Grafik wurde aktualisiert.", "Grafik ändern",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetStartInstruction_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            if (!vm.HasCustomStartInstruction)
                return;

            if (MessageBox.Show(
                    "Soll die benutzerdefinierte Grafik durch die Standardgrafik ersetzt werden?",
                    "Standardgrafik wiederherstellen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            if (!vm.TryResetStartInstructionGraphic(out var error))
            {
                MessageBox.Show(error, "Standardgrafik wiederherstellen",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Die Standardgrafik ist wieder aktiv.", "Standardgrafik wiederherstellen",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ChangeWarningInfo_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            var file = SelectGraphicFile("Grafik für \"Wichtiger Hinweis\" auswählen");
            if (string.IsNullOrEmpty(file))
                return;

            if (!vm.TryUpdateWarningInfoGraphic(file, out var error))
            {
                MessageBox.Show(error, "Grafik ändern", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Die Grafik wurde aktualisiert.", "Grafik ändern",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetWarningInfo_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            if (!vm.HasCustomWarningInfo)
                return;

            if (MessageBox.Show(
                    "Soll die benutzerdefinierte Grafik durch die Standardgrafik ersetzt werden?",
                    "Standardgrafik wiederherstellen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            if (!vm.TryResetWarningInfoGraphic(out var error))
            {
                MessageBox.Show(error, "Standardgrafik wiederherstellen",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Die Standardgrafik ist wieder aktiv.", "Standardgrafik wiederherstellen",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
