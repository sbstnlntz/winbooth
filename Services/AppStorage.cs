// Provides helper methods for locating and creating application data directories.

using System;
using System.IO;

namespace winbooth.Services
{
    internal static class AppStorage
    {
        private static readonly string AppRoot = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string AppDataRoot = Path.Combine(AppRoot, "AppData");

        public static string EnsureDirectory(string relativePath = null)
        {
            var target = string.IsNullOrWhiteSpace(relativePath)
                ? AppDataRoot
                : Path.Combine(AppDataRoot, relativePath);
            Directory.CreateDirectory(target);
            return target;
        }

        public static string GetFilePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("relativePath darf nicht leer sein.", nameof(relativePath));

            var directory = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(Path.Combine(AppDataRoot, directory));
            }
            else
            {
                Directory.CreateDirectory(AppDataRoot);
            }

            return Path.Combine(AppDataRoot, relativePath);
        }
    }
}
