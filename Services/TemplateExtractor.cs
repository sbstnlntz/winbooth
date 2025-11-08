using System.IO;
using System.IO.Compression;

namespace winbooth.Services {
    public static class TemplateExtractor {
        public static void ExtractTemplateZip(string zipPath, string extractToFolder) {
            if (!Directory.Exists(extractToFolder)) {
                Directory.CreateDirectory(extractToFolder);
            }

            ZipFile.ExtractToDirectory(zipPath, extractToFolder, true);
        }
    }
}
