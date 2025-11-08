using System.Windows;
using System.Windows.Controls;

namespace winbooth.Views
{
    public partial class PinPadDialog : Window
    {
        public string EnteredPin { get; private set; } = string.Empty;

        public PinPadDialog()
        {
            InitializeComponent();
            PinBox.Password = string.Empty;
            PinBox.IsEnabled = false; // Eingabe erfolgt ausschließlich über das Keypad
        }

        private void Number_Click(object sender, RoutedEventArgs e)
        {
            if (EnteredPin.Length >= 4)
                return;

            if (sender is Button btn && btn.Content is string digit)
                EnteredPin += digit;

            PinBox.Password = EnteredPin;
            ErrorText.Text = string.Empty;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (EnteredPin.Length > 0)
            {
                EnteredPin = EnteredPin[..^1];
                PinBox.Password = EnteredPin;
            }

            ErrorText.Text = string.Empty;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (EnteredPin.Length != 4)
            {
                ErrorText.Text = "Bitte 4 Ziffern eingeben!";
                return;
            }

            DialogResult = true;
        }
    }
}
