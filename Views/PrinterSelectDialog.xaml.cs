using System.Collections.ObjectModel;
using System.Windows;

namespace FotoboxApp.Views
{
    public partial class PrinterSelectDialog : Window
    {
        public string SelectedPrinter { get; private set; }

        public PrinterSelectDialog(ObservableCollection<string> printers, string currentPrinter)
        {
            InitializeComponent();
            ListBoxPrinters.ItemsSource = printers;
            if (!string.IsNullOrEmpty(currentPrinter))
                ListBoxPrinters.SelectedItem = currentPrinter;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (ListBoxPrinters.SelectedItem != null)
            {
                SelectedPrinter = ListBoxPrinters.SelectedItem.ToString();
                this.DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
