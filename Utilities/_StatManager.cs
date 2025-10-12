using System;
using System.IO;

namespace FotoboxApp.Utilities
{
    public static class StatManager
    {
        private static string StatsFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fotobox", "counter.txt");

        public static int GetTotalPhotoCount()
        {
            try
            {
                if (File.Exists(StatsFile))
                {
                    var content = File.ReadAllText(StatsFile);
                    if (int.TryParse(content, out int result))
                        return result;
                }
            }
            catch { }
            return 0;
        }

        public static void IncreaseTotalPhotoCount()
        {
            int current = GetTotalPhotoCount();
            try
            {
                File.WriteAllText(StatsFile, (current + 1).ToString());
            }
            catch { }
        }

        public static void ResetTotalPhotoCount()
        {
            try
            {
                File.WriteAllText(StatsFile, "0");
            }
            catch { }
        }
    }
}
