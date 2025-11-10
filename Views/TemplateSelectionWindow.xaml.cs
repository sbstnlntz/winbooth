// Hosts the pop-up template browser and surfaces selection events back to the calling view.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using winbooth.Models;
using winbooth.ViewModels;

namespace winbooth.Views
{
    public partial class TemplateSelectionWindow : Window
    {
        private readonly StartViewModel _viewModel;
        private readonly ObservableCollection<TemplateItem> _templates = new();

        public TemplateItem SelectedTemplate { get; private set; }

        public TemplateSelectionWindow(StartViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            TemplatesItemsControl.ItemsSource = _templates;
            RefreshTemplateList();
        }

        private void Template_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TemplateItem item)
            {
                SelectedTemplate = item;
                DialogResult = true;
            }
        }

        private async void UploadTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Eigenes Design auswählen",
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
                // optional initial-directory hint only
            }

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var result = await _viewModel.ImportTemplatesFromFilesAsync(dialog.FileNames);
            ShowTemplateImportFeedback(result);

            if (result == null || !result.HasChanges)
            {
                return;
            }

            await _viewModel.WaitForTemplateReloadAsync();
            RefreshTemplateList();

            var importedName = result.ImportedTemplates.FirstOrDefault()
                               ?? result.UpdatedTemplates.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(importedName))
            {
                var template = _viewModel.FindTemplateByName(importedName);
                if (template != null)
                {
                    SelectedTemplate = template;
                    DialogResult = true;
                }
            }
        }

        private void ShowTemplateImportFeedback(TemplateImportResult result)
        {
            if (result == null)
            {
                return;
            }

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
                    .Where(n => !string.IsNullOrWhiteSpace(n))
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
                    .Select(f => Path.GetFileName(f.File))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
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
                MessageBox.Show("Keine Änderungen am Design-Bestand.", "Design-Import",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var icon = result.FailedFiles.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
            MessageBox.Show(string.Join(Environment.NewLine, messages), "Design-Import",
                MessageBoxButton.OK, icon);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OverlayBackground_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ContentBorder == null)
            {
                DialogResult = false;
                return;
            }

            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (ReferenceEquals(source, ContentBorder))
                {
                    return;
                }
                source = VisualTreeHelper.GetParent(source);
            }

            DialogResult = false;
        }

        private void RefreshTemplateList()
        {
            if (_viewModel == null)
            {
                return;
            }

            var templates = _viewModel.GetTemplateLibraryTemplates() ?? Array.Empty<TemplateItem>();
            _templates.Clear();
            foreach (var template in templates)
            {
                _templates.Add(template);
            }
        }
    }
}
