using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace winbooth.Views
{
    public partial class PrinterSelectDialog : Window
    {
        public sealed class DeviceOption : INotifyPropertyChanged
        {
            private bool _isSelected;

            public DeviceOption(string name, bool isSelected)
            {
                Name = name;
                _isSelected = isSelected;
            }

            public string Name { get; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value)
                    {
                        return;
                    }

                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private readonly List<DeviceOption> _options;

        public IReadOnlyList<DeviceOption> Options => _options;

        public string SelectedPrinter { get; private set; } = string.Empty;

        public PrinterSelectDialog(IEnumerable<string> printers, string currentPrinter)
        {
            InitializeComponent();

            _options = printers?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .Select(p => new DeviceOption(p, string.Equals(p, currentPrinter, StringComparison.Ordinal)))
                .ToList() ?? new List<DeviceOption>();

            if (_options.Count == 0)
            {
                _options.Add(new DeviceOption("Keine Drucker gefunden", false));
            }

            DataContext = this;

            Loaded += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(currentPrinter) && _options.Count > 0 && !_options[0].Name.Equals("Keine Drucker gefunden", StringComparison.Ordinal))
                {
                    _options[0].IsSelected = true;
                }

                EnsureRadioSelectionSync();
            };
        }

        private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListBox listBox)
            {
                return;
            }

            var selectedOption = listBox.SelectedItem as DeviceOption;
            foreach (var option in _options)
            {
                option.IsSelected = option == selectedOption;
            }

            EnsureRadioSelectionSync();
        }

        private void EnsureRadioSelectionSync()
        {
            var firstSelected = _options.FirstOrDefault(o => o.IsSelected);
            if (firstSelected == null)
            {
                return;
            }

            ItemsList.SelectedItem = firstSelected;
        }

        private void DeviceRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton radioButton || radioButton.DataContext is not DeviceOption option)
            {
                return;
            }

            foreach (var opt in _options)
            {
                opt.IsSelected = ReferenceEquals(opt, option);
            }

            EnsureRadioSelectionSync();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var chosen = _options.FirstOrDefault(o => o.IsSelected && !string.Equals(o.Name, "Keine Drucker gefunden", StringComparison.Ordinal));
            if (chosen == null)
            {
                MessageBox.Show(
                    "Bitte einen Drucker auswählen.",
                    "Drucker auswählen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SelectedPrinter = chosen.Name;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}


