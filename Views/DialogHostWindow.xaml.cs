// Generic window wrapper for hosting full screen dialogs with consistent chrome and focus rules.

using System.Windows;
using System.Windows.Controls;

namespace winbooth.Views
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
