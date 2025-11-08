// Records the outcome of template import operations, including success flags and errors.

using System.Collections.Generic;

namespace winbooth.Models
{
    public sealed class TemplateImportResult
    {
        public List<string> ImportedTemplates { get; } = new();
        public List<string> UpdatedTemplates { get; } = new();
        public List<string> InvalidFiles { get; } = new();
        public List<(string File, string Error)> FailedFiles { get; } = new();

        public bool HasChanges => ImportedTemplates.Count > 0 || UpdatedTemplates.Count > 0;
    }
}
