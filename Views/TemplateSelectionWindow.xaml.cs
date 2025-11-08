// Hosts the pop-up template browser and surfaces selection events back to the calling view.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using winbooth.Models;

namespace winbooth.Views
{
    public partial class TemplateSelectionWindow : Window
    {
        public TemplateItem SelectedTemplate { get; private set; }

        public TemplateSelectionWindow(IEnumerable<TemplateItem> templates)
        {
            InitializeComponent();
            DataContext = templates;
        }

        private void Template_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TemplateItem item)
            {
                SelectedTemplate = item;
                DialogResult = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
