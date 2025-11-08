// Handles admin configuration of default templates, slot assignments, and preview refresh.

using System.Windows;
using System.Windows.Controls;
using winbooth.ViewModels;

namespace winbooth.Views
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
