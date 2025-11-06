using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FotoboxApp.Models;
using FotoboxApp.ViewModels;

namespace FotoboxApp.Views
{
    public partial class DesignManagementWindow : Window
    {
        private readonly StartViewModel _viewModel;

        public ObservableCollection<TemplateOption> TemplateOptions { get; } = new();

        public sealed class TemplateOption : INotifyPropertyChanged
        {
            private bool _isAllowed;

            public TemplateOption(TemplateItem template, bool isAllowed)
            {
                Template = template;
                _isAllowed = isAllowed;
            }

            public TemplateItem Template { get; }

            public bool IsAllowed
            {
                get => _isAllowed;
                set
                {
                    if (_isAllowed == value)
                        return;

                    _isAllowed = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAllowed)));
                }
            }

            public string Name => Template?.Name ?? "Unbekanntes Design";

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public DesignManagementWindow(StartViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            _viewModel.RefreshTemplatesFromDisk();
            SyncTemplateOptions();
        }

        private void SyncTemplateOptions()
        {
            var allowedNames = new HashSet<string>(
                _viewModel.AllowedTemplateNames ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var hasExplicitAllowed = allowedNames.Count > 0;

            TemplateOptions.Clear();
            foreach (var template in _viewModel.Templates)
            {
                var isAllowed = !hasExplicitAllowed || allowedNames.Contains(template.Name);
                TemplateOptions.Add(new TemplateOption(template, isAllowed));
            }
        }

        private void PersistAllowedTemplates()
        {
            var selectedNames = TemplateOptions
                .Where(option => option.IsAllowed && !string.IsNullOrWhiteSpace(option.Template?.Name))
                .Select(option => option.Template!.Name)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            _viewModel.UpdateAllowedTemplates(selectedNames);
        }

        private void TemplateAllowed_Toggled(object sender, RoutedEventArgs e)
        {
            PersistAllowedTemplates();
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
            PersistAllowedTemplates();
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
            PersistAllowedTemplates();
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
            MessageBox.Show(message, "Designs verwalten", MessageBoxButton.OK, icon);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
