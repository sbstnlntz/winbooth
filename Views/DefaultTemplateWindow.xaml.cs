using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using FotoboxApp.Models;
using FotoboxApp.ViewModels;

namespace FotoboxApp.Views
{
    public partial class DefaultTemplateWindow : Window
    {
        private readonly StartViewModel _viewModel;
        private bool _suppressSelectionUpdates;

        public ObservableCollection<TemplateOption> TemplateOptions { get; } = new();

        public sealed class TemplateOption : INotifyPropertyChanged
        {
            private bool _isSelected;

            public TemplateOption(TemplateItem template, bool isSelected)
            {
                Template = template;
                _isSelected = isSelected;
            }

            public TemplateItem Template { get; }

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

            public string Name => Template?.Name ?? "Unbekanntes Design";

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public DefaultTemplateWindow(StartViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            _viewModel.RefreshTemplatesFromDisk();
            SyncTemplateOptions();
        }

        private void SyncTemplateOptions()
        {
            var defaultName = _viewModel.DefaultTemplateName ?? string.Empty;
            var selectedNameLookup = new HashSet<string>(StringComparer.Ordinal) { defaultName };

            _suppressSelectionUpdates = true;
            TemplateOptions.Clear();
            foreach (var template in _viewModel.Templates)
            {
                var isSelected = selectedNameLookup.Contains(template.Name);
                TemplateOptions.Add(new TemplateOption(template, isSelected));
            }
            _suppressSelectionUpdates = false;
        }

        private void TemplateSelected_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectionUpdates)
                return;

            if (sender is not ToggleButton toggle ||
                toggle.DataContext is not TemplateOption option ||
                option.Template == null)
            {
                return;
            }

            _suppressSelectionUpdates = true;
            foreach (var item in TemplateOptions)
            {
                item.IsSelected = ReferenceEquals(item, option);
            }
            _suppressSelectionUpdates = false;

            try
            {
                _viewModel.SetDefaultTemplateByName(option.Template.Name);
            }
            finally
            {
                SyncTemplateOptions();
            }
        }

        private void ImportDesign_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Designs hinzufügen",
                Filter = "Design-Pakete (*.zip)|*.zip",
                Multiselect = true,
                CheckFileExists = true
            };

            try
            {
                var templatesRoot = StartViewModel.GetTemplatesRootPath();
                if (!string.IsNullOrWhiteSpace(templatesRoot) && Directory.Exists(templatesRoot))
                {
                    dialog.InitialDirectory = templatesRoot;
                }
            }
            catch
            {
                // Initial directory fallback is best-effort.
            }

            if (dialog.ShowDialog(this) != true)
                return;

            var result = _viewModel.ImportTemplatesFromFiles(dialog.FileNames);
            SyncTemplateOptions();
            ShowTemplateImportResult(result);
        }

        private static void ShowTemplateImportResult(StartViewModel.TemplateImportResult result)
        {
            if (result == null)
                return;

            var messages = new List<string>();

            if (result.ImportedTemplates.Count > 0)
            {
                messages.Add(result.ImportedTemplates.Count == 1
                    ? $"1 neues Design importiert: {result.ImportedTemplates[0]}"
                    : $"{result.ImportedTemplates.Count} neue Designs importiert.");
            }

            if (result.UpdatedTemplates.Count > 0)
            {
                messages.Add(result.UpdatedTemplates.Count == 1
                    ? $"1 vorhandenes Design aktualisiert: {result.UpdatedTemplates[0]}"
                    : $"{result.UpdatedTemplates.Count} vorhandene Designs aktualisiert.");
            }

            if (result.InvalidFiles.Count > 0)
            {
                var invalidNames = result.InvalidFiles
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();

                if (invalidNames.Count > 0)
                {
                    const int maxListEntries = 3;
                    var displayNames = invalidNames.Take(maxListEntries).ToList();
                    var remainder = invalidNames.Count - displayNames.Count;
                    var summary = string.Join(", ", displayNames);
                    if (remainder > 0)
                    {
                        summary += $" +{remainder}";
                    }

                    messages.Add(result.InvalidFiles.Count == 1
                        ? $"1 Datei übersprungen (kein ZIP): {summary}"
                        : $"{result.InvalidFiles.Count} Dateien übersprungen (kein ZIP): {summary}");
                }
                else
                {
                    messages.Add(result.InvalidFiles.Count == 1
                        ? "1 Datei übersprungen (kein ZIP)."
                        : $"{result.InvalidFiles.Count} Dateien übersprungen (kein ZIP).");
                }
            }

            if (result.FailedFiles.Count > 0)
            {
                var failedNames = result.FailedFiles
                    .Select(failure => Path.GetFileName(failure.File))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();

                if (failedNames.Count > 0)
                {
                    const int maxListEntries = 3;
                    var displayNames = failedNames.Take(maxListEntries).ToList();
                    var remainder = failedNames.Count - displayNames.Count;
                    var summary = string.Join(", ", displayNames);
                    if (remainder > 0)
                    {
                        summary += $" +{remainder}";
                    }

                    messages.Add(result.FailedFiles.Count == 1
                        ? $"1 Datei konnte nicht übernommen werden: {summary}"
                        : $"{result.FailedFiles.Count} Dateien konnten nicht übernommen werden: {summary}");
                }
                else
                {
                    messages.Add(result.FailedFiles.Count == 1
                        ? "1 Datei konnte nicht übernommen werden."
                        : $"{result.FailedFiles.Count} Dateien konnten nicht übernommen werden.");
                }
            }

            if (messages.Count == 0)
            {
                messages.Add("Es wurden keine gültigen Designs ausgewählt.");
            }

            var message = string.Join(Environment.NewLine + Environment.NewLine, messages);
            var icon = result.FailedFiles.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
            MessageBox.Show(message, "Standard-Design auswählen", MessageBoxButton.OK, icon);
        }

        private void DeleteDesignButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not TemplateItem template || template == null)
                return;

            var confirm = MessageBox.Show(
                $"Soll das Design \"{template.Name}\" dauerhaft gelöscht werden?",
                "Design löschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
                return;

            if (!_viewModel.TryDeleteTemplate(template, out var errorMessage))
            {
                MessageBox.Show(errorMessage ?? "Löschen fehlgeschlagen.", "Design löschen",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"Design \"{template.Name}\" wurde entfernt.", "Design löschen",
                MessageBoxButton.OK, MessageBoxImage.Information);

            _viewModel.RefreshTemplatesFromDisk();
            SyncTemplateOptions();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
