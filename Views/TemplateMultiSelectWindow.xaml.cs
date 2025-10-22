using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using FotoboxApp.Models;

namespace FotoboxApp.Views
{
    public partial class TemplateMultiSelectWindow : Window
    {
        public sealed class TemplateOption : INotifyPropertyChanged
        {
            private bool _isSelected;

            public TemplateOption(TemplateItem template, bool isSelected)
            {
                Template = template;
                _isSelected = isSelected;
            }

            public TemplateItem Template { get; }

            public string Name => Template?.Name ?? "Unbekanntes Design";

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

        private readonly List<TemplateOption> _options;

        public IReadOnlyList<TemplateOption> Options => _options;

        public bool HasTemplates => _options.Any(o => o.Template != null);

        public IReadOnlyList<string> SelectedTemplateNames { get; private set; } = Array.Empty<string>();

        public TemplateMultiSelectWindow(IEnumerable<TemplateItem> templates, IEnumerable<string> preselectedNames)
        {
            InitializeComponent();

            var preselection = new HashSet<string>(
                preselectedNames?.Where(n => !string.IsNullOrWhiteSpace(n)) ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            _options = templates?
                .Where(t => t != null)
                .Select(t => new TemplateOption(t, preselection.Contains(t.Name)))
                .ToList() ?? new List<TemplateOption>();

            DataContext = this;

            Loaded += (_, _) =>
            {
                if (!HasTemplates)
                {
                    return;
                }

                if (_options.All(o => !o.IsSelected) && _options.Count > 0)
                {
                    // Preselect the first item for quicker touch interaction
                    _options[0].IsSelected = true;
                }
            };
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var selected = _options
                .Where(o => o.IsSelected && o.Template != null)
                .Select(o => o.Template.Name)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    "Bitte mindestens ein Design auswählen.",
                    "Design-Auswahl",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SelectedTemplateNames = selected;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
