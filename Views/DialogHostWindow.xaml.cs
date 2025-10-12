using System.Windows;
using System.Windows.Controls;

namespace FotoboxApp.Views
{
    public partial class DialogHostWindow : Window
    {
        public DialogHostWindow(UserControl content)
        {
            InitializeComponent();
            HostContent.Content = content;
        }
    }
}
