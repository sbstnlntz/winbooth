using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FotoboxApp.Models;

namespace FotoboxApp.Views
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
