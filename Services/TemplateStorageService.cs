using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using winbooth.Models;

namespace winbooth.Services
{
    public sealed class TemplateStorageService
    {
        public static TemplateStorageService Instance { get; } = new();

        private readonly string _templatesRootPath;
        private readonly string _legacyTemplatesRootPath;
        private readonly string _defaultTemplatesRootPath;
        private readonly string _legacyDefaultTemplatesRootPath;
        private readonly SemaphoreSlim _importSemaphore = new(1, 1);

        private readonly Task _templatesMigrationTask;
        private readonly Task _defaultTemplatesMigrationTask;

        private TemplateStorageService()
        {
            _templatesRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
            _defaultTemplatesRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "default_templates");

            Directory.CreateDirectory(_templatesRootPath);
            Directory.CreateDirectory(_defaultTemplatesRootPath);

            var picturesRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            _legacyTemplatesRootPath = Path.Combine(picturesRoot, "Fotobox", "templates");
            _legacyDefaultTemplatesRootPath = Path.Combine(picturesRoot, "Fotobox", "default_templates");

            _templatesMigrationTask = Task.Run(() => MigrateLegacyContent(_legacyTemplatesRootPath, _templatesRootPath));
            _defaultTemplatesMigrationTask = Task.Run(() => MigrateLegacyContent(_legacyDefaultTemplatesRootPath, _defaultTemplatesRootPath));
        }

        public string TemplatesRootPath => _templatesRootPath;
        public string DefaultTemplatesRootPath => _defaultTemplatesRootPath;

        public async Task EnsureLegacyMigrationCompletedAsync(CancellationToken token = default)
        {
            await _templatesMigrationTask.WaitAsync(token).ConfigureAwait(false);
            await _defaultTemplatesMigrationTask.WaitAsync(token).ConfigureAwait(false);
        }

        public async Task<TemplateImportResult> ImportTemplatesAsync(IEnumerable<string> filePaths, string targetFolder, CancellationToken token = default)
        {
            if (filePaths == null)
                return new TemplateImportResult();

            await _importSemaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await EnsureTargetFolderReadyAsync(targetFolder, token).ConfigureAwait(false);
                return await Task.Run(() => CopyPackages(filePaths, targetFolder), token).ConfigureAwait(false);
            }
            finally
            {
                _importSemaphore.Release();
            }
        }

        private static TemplateImportResult CopyPackages(IEnumerable<string> filePaths, string targetFolder)
        {
            var result = new TemplateImportResult();
            Directory.CreateDirectory(targetFolder);

            foreach (var filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    if (!string.IsNullOrWhiteSpace(filePath))
                        result.InvalidFiles.Add(filePath);
                    continue;
                }

                if (!string.Equals(Path.GetExtension(filePath), ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    result.InvalidFiles.Add(filePath);
                    continue;
                }

                try
                {
                    var destName = Path.GetFileName(filePath);
                    if (string.IsNullOrWhiteSpace(destName))
                    {
                        result.InvalidFiles.Add(filePath);
                        continue;
                    }

                    var templateName = Path.GetFileNameWithoutExtension(destName);
                    if (string.IsNullOrWhiteSpace(templateName))
                    {
                        result.InvalidFiles.Add(filePath);
                        continue;
                    }

                    var destPath = Path.Combine(targetFolder, destName);
                    var sourceFullPath = Path.GetFullPath(filePath);
                    var destFullPath = Path.GetFullPath(destPath);

                    if (string.Equals(sourceFullPath, destFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(destFullPath))
                            result.UpdatedTemplates.Add(templateName);
                        continue;
                    }

                    var wasExisting = File.Exists(destPath);
                    File.Copy(filePath, destPath, true);

                    if (wasExisting)
                        result.UpdatedTemplates.Add(templateName);
                    else
                        result.ImportedTemplates.Add(templateName);
                }
                catch (Exception ex)
                {
                    result.FailedFiles.Add((filePath, ex.Message));
                }
            }

            return result;
        }

        private static async Task EnsureTargetFolderReadyAsync(string targetFolder, CancellationToken token)
        {
            await Task.Run(() => Directory.CreateDirectory(targetFolder), token).ConfigureAwait(false);
        }

        private static void MigrateLegacyContent(string legacyFolder, string targetFolder)
        {
            if (!Directory.Exists(legacyFolder))
                return;

            try
            {
                Directory.CreateDirectory(targetFolder);
                foreach (var sourceFile in Directory.GetFiles(legacyFolder, "*.zip"))
                {
                    try
                    {
                        var destination = Path.Combine(targetFolder, Path.GetFileName(sourceFile) ?? string.Empty);
                        if (string.IsNullOrWhiteSpace(destination))
                            continue;

                        if (!File.Exists(destination))
                        {
                            File.Copy(sourceFile, destination, overwrite: false);
                        }
                    }
                    catch
                    {
                        // best effort
                    }
                }
            }
            catch
            {
                // migration best effort
            }
        }
    }
}
