// Handles gallery backups, find and restore operations, and housekeeping tasks.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace winbooth.Services
{
    public static class BackupService
    {
        private static string PicturesRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fotobox");

        private static string AppFolderRoot => AppDomain.CurrentDomain.BaseDirectory;

        public static string GetBackupRoot()
        {
            var root = Path.Combine(AppFolderRoot, "Backups");
            Directory.CreateDirectory(root);
            return root;
        }

        public static string CreateBackup(string galleryName)
        {
            if (string.IsNullOrWhiteSpace(galleryName))
                throw new ArgumentException("Galerie-Name ist leer", nameof(galleryName));

            var galleryDir = Path.Combine(PicturesRoot, galleryName);
            if (!Directory.Exists(galleryDir))
                throw new DirectoryNotFoundException($"Galerie-Ordner nicht gefunden: {galleryDir}");

            var backupRoot = GetBackupRoot();
            var backupName = $"{Sanitize(galleryName)}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            var backupPath = Path.Combine(backupRoot, backupName);

            ZipFile.CreateFromDirectory(galleryDir, backupPath, CompressionLevel.SmallestSize, includeBaseDirectory: false);

            return backupPath;
        }

        public static IEnumerable<FileInfo> ListBackups()
        {
            var root = GetBackupRoot();
            var di = new DirectoryInfo(root);
            if (!di.Exists) yield break;
            foreach (var f in di.GetFiles("*.zip"))
                yield return f;
        }

        public static bool BackupExists(string galleryName)
        {
            if (string.IsNullOrWhiteSpace(galleryName))
                return false;

            var pattern = $"{Sanitize(galleryName)}_*.zip";
            return Directory.EnumerateFiles(GetBackupRoot(), pattern, SearchOption.TopDirectoryOnly).Any();
        }

        public static void DeleteGalleryDirectory(string galleryName)
        {
            if (string.IsNullOrWhiteSpace(galleryName))
                return;

            var galleryDir = Path.Combine(PicturesRoot, galleryName);
            DeleteDirectorySafe(galleryDir);
        }

        public static void DeleteBackupFile(string backupFilePath)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath))
                return;

            if (!File.Exists(backupFilePath))
                return;

            File.Delete(backupFilePath);
        }

        public static void RestoreBackup(string backupFilePath, string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath))
                throw new ArgumentException("Backup-Pfad ist leer", nameof(backupFilePath));
            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Backup-Datei nicht gefunden", backupFilePath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
                throw new ArgumentException("Zielordner ist leer", nameof(targetDirectory));

            var targetDir = Path.GetFullPath(targetDirectory);

            if (Directory.Exists(targetDir))
                DeleteDirectorySafe(targetDir);
            Directory.CreateDirectory(targetDir);
            ZipFile.ExtractToDirectory(backupFilePath, targetDir, overwriteFiles: true);
        }

        private static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        private static void DeleteDirectorySafe(string directory)
        {
            if (!Directory.Exists(directory))
                return;

            try
            {
                Directory.Delete(directory, true);
            }
            catch
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    try { File.Delete(file); }
                    catch { }
                }

                foreach (var dir in Directory.GetDirectories(directory))
                {
                    try { Directory.Delete(dir, true); }
                    catch { }
                }

                try { Directory.Delete(directory, true); }
                catch { }
            }
        }
    }
}
