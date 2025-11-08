// Encapsulates StartViewModel helper methods for managing allowed device lists and validation.

using System;
using System.Collections.Generic;
using System.Linq;

namespace winbooth.ViewModels
{
    public partial class StartViewModel
    {
        private readonly List<string> _allowedCameraNames = new();
        private readonly List<string> _allowedPrinterNames = new();
        private readonly List<string> _allowedTemplateNames = new();

        public IReadOnlyList<string> AllowedCameraNames => _allowedCameraNames;
        public IReadOnlyList<string> AllowedPrinterNames => _allowedPrinterNames;
        public IReadOnlyList<string> AllowedTemplateNames => _allowedTemplateNames;

        public string AllowedCamerasSummary => BuildAllowedSummary(_allowedCameraNames, AvailableCameras, "Keine Geräte gefunden");
        public string AllowedPrintersSummary => BuildAllowedSummary(_allowedPrinterNames, AvailablePrinters, "Keine Geräte gefunden");
        public string AllowedTemplatesSummary => BuildAllowedSummary(_allowedTemplateNames, Templates.Select(t => t.Name), "Keine Designs gefunden");

        private static string BuildAllowedSummary(IReadOnlyCollection<string> allowed, IEnumerable<string> availableEnumerable, string emptyMessage)
        {
            var availableList = availableEnumerable?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            if (availableList.Count == 0)
            {
                return emptyMessage;
            }

            var availableLookup = new HashSet<string>(availableList, StringComparer.Ordinal);
            var allowedList = allowed?
                .Where(availableLookup.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            var allowedCount = allowedList.Count;
            if (allowedCount == 0 || allowedCount >= availableList.Count && !availableList.Except(allowedList).Any())
            {
                return "Alle verfügbar";
            }

            const int maxItemsToShow = 3;
            if (allowedCount <= maxItemsToShow)
            {
                return string.Join(", ", allowedList);
            }

            var displayItems = allowedList.Take(maxItemsToShow);
            return $"{string.Join(", ", displayItems)} +{allowedCount - maxItemsToShow} weitere";
        }

        private static void NormalizeAllowedDevices(List<string> target, IEnumerable<string> availableEnumerable)
        {
            if (target == null)
            {
                return;
            }

            var availableList = availableEnumerable?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            var selectedLookup = new HashSet<string>(target, StringComparer.Ordinal);
            var normalized = availableList.Where(selectedLookup.Contains).ToList();

            if (normalized.Count == 0 && availableList.Count > 0)
            {
                normalized = availableList;
            }

            target.Clear();
            target.AddRange(normalized);
        }

        private static void ApplyAllowedSelection(List<string> target, IEnumerable<string> selected, IEnumerable<string> availableEnumerable)
        {
            target.Clear();
            var availableList = availableEnumerable?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            if (selected != null)
            {
                var requested = new HashSet<string>(
                    selected.Where(s => !string.IsNullOrWhiteSpace(s)),
                    StringComparer.Ordinal);

                foreach (var item in availableList)
                {
                    if (requested.Contains(item))
                    {
                        target.Add(item);
                    }
                }
            }

            if (target.Count == 0 && availableList.Count > 0)
            {
                target.AddRange(availableList);
            }
        }

        private static IReadOnlyList<string> GetSelectableDevices(List<string> target, IEnumerable<string> availableEnumerable)
        {
            var availableList = availableEnumerable?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            if (availableList.Count == 0)
            {
                return Array.Empty<string>();
            }

            var effective = target.Count > 0
                ? target.Where(name => availableList.Contains(name)).ToList()
                : availableList;

            return effective.Count > 0 ? effective : availableList;
        }

        private static void TryPopulateList(List<string> target, Func<IReadOnlyList<string>> loader)
        {
            if (target == null || loader == null)
                return;

            try
            {
                var items = loader();
                if (items != null)
                    target.AddRange(items);
            }
            catch
            {
                // ignore load errors
            }
        }
    }
}
