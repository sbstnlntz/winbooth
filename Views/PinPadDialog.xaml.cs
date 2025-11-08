using System.Windows;
using System.Windows.Controls;


namespace winbooth.Views
{
    public partial class PinPadDialog : Window
    {
        public string EnteredPin { get; private set; } = "";
        private int _failedAttempts = 0;

        public PinPadDialog()
        {
            InitializeComponent();
            PinBox.Password = "";
            PinBox.IsEnabled = false; // Wir steuern Eingabe über das Keypad
        }

        private void Number_Click(object sender, RoutedEventArgs e)
        {
            if (EnteredPin.Length >= 4) return;
            if (sender is Button btn && btn.Content is string digit)
                EnteredPin += digit;
            PinBox.Password = EnteredPin;
            ErrorText.Text = "";
        }


        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (EnteredPin.Length > 0)
            {
                EnteredPin = EnteredPin.Substring(0, EnteredPin.Length - 1);
                PinBox.Password = EnteredPin;
            }
            ErrorText.Text = "";
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
