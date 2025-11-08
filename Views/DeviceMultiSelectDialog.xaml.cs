// Allows selecting multiple hardware devices such as cameras, printers, or USB drives.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace winbooth.Views
{
    public partial class DeviceMultiSelectDialog : Window
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
                        return;

                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private readonly List<DeviceOption> _options;

        public IReadOnlyList<DeviceOption> Options => _options;

        public IReadOnlyList<string> SelectedItems { get; private set; } = Array.Empty<string>();

        public DeviceMultiSelectDialog(
            IEnumerable<string> items,
            IEnumerable<string> preselectedItems,
            string titleText)
        {
            InitializeComponent();

            var availableItems = items?.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().ToList()
                               ?? new List<string>();

            var preselection = new HashSet<string>(
                preselectedItems?.Where(i => !string.IsNullOrWhiteSpace(i)) ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            _options = availableItems
                .Select(name => new DeviceOption(name, preselection.Contains(name)))
                .ToList();

            if (_options.Count == 0)
            {
                _options.Add(new DeviceOption("Keine Geräte gefunden", false));
            }

            Title = titleText ?? "Geräte auswählen";
            TitleLabel.Text = Title;
            DataContext = this;

            Loaded += (_, _) =>
            {
                if (_options.All(o => !o.IsSelected) && _options.Count > 0 && !_options[0].Name.Equals("Keine Geräte gefunden", StringComparison.Ordinal))
                {
                    _options[0].IsSelected = true;
                }
            };
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var selected = _options
                .Where(o => o.IsSelected && !string.Equals(o.Name, "Keine Geräte gefunden", StringComparison.Ordinal))
                .Select(o => o.Name)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    "Bitte mindestens ein Gerät auswählen.",
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SelectedItems = selected;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}


