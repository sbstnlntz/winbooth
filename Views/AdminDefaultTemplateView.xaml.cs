using System.Windows;
using System.Windows.Controls;
using FotoboxApp.ViewModels;

namespace FotoboxApp.Views
{
    public partial class AdminDefaultTemplateView : UserControl
    {
        public AdminDefaultTemplateView()
        {
            InitializeComponent();
        }

        public AdminDefaultTemplateView(StartViewModel vm) : this()
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

        private void ClearDefault_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not StartViewModel vm)
                return;

            vm.SetDefaultTemplateByName(string.Empty);
        }
    }
}
