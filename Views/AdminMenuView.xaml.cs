using System.Windows;
using System.Windows.Controls;
using FotoboxApp.ViewModels;

namespace FotoboxApp.Views
{
    public partial class AdminMenuView : UserControl
    {
        public AdminMenuView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Overload, um StartViewModel direkt zu übernehmen.
        /// </summary>
        public AdminMenuView(StartViewModel vm)
            : this()
        {
            DataContext = vm;
            // Optional: Buchungsnummer und Fotos aus VM befüllen:
            TbBookingNumber.Text = vm.GalleryName;     // oder eigene Property
            TbPhotoCount.Text = vm.PreviewDurationSeconds.ToString();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is StartViewModel vm)) return;
            if (Application.Current.MainWindow is MainWindow wnd)
            {
                wnd.MainFrame.Navigate(new StartView(vm));
            }
        }

        private void NewEvent_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Neues Event angelegt und alte Daten archiviert.",
                            "Admin", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenArchive_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Archiv-Ordner geöffnet.",
                            "Admin", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteDesign_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Design entfernt.",
                            "Admin", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddDesign_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Neues Design hinzugefügt.",
                            "Admin", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
