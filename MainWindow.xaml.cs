using System.Windows;
using System.Windows.Input;
using FotoboxApp.ViewModels;
using FotoboxApp.Views;

namespace FotoboxApp
{
    public partial class MainWindow : Window
    {
        private readonly StartViewModel _mainViewModel = new StartViewModel();

        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(new StartView(_mainViewModel)); // IMMER dieselbe Instanz
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        // Property für globale ViewModel-Nutzung in allen Views!
        public StartViewModel MainViewModel => _mainViewModel;

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                && (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                && e.Key == Key.Q)
            {
                // Wieder auf Startseite, mit gleichem ViewModel!
                MainFrame.Navigate(new StartView(_mainViewModel));
                e.Handled = true;
            }
        }
    }
}
