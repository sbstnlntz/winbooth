using System.Collections.Generic;
using System.Management;
using Emgu.CV;

namespace FotoboxApp.Services
{
    public static class CameraHelper
    {
        public static List<string> GetAllCameraNames()
        {
            var names = new List<string>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE " +
                    "(Description LIKE '%Camera%' OR " +
                    "Description LIKE '%Imaging%' OR " +
                    "Caption LIKE '%Camera%' OR " +
                    "Caption LIKE '%Imaging%' OR " +
                    "Caption LIKE '%Video%')"))
                {
                    foreach (var device in searcher.Get())
                    {
                        string name = device["Caption"]?.ToString() ?? "Unbekanntes Gerät";
                        if (!names.Contains(name))
                            names.Add(name);
                    }
                }
            }
            catch { /* Ignorieren */ }

            // --- Fallback: Gibt es zumindest ein VideoCapture(0)? ---
            if (names.Count == 0)
            {
                try
                {
                    using (var cap = new VideoCapture(0))
                    {
                        if (cap.IsOpened)
                        {
                            names.Add("Standard-Kamera (Video0)");
                        }
                    }
                }
                catch { /* Nichts */ }
            }
            return names;
        }

        public static string GetConnectedCameraName()
        {
            var all = GetAllCameraNames();
            return all.Count > 0 ? all[0] : "Keine Kamera gefunden";
        }
    }
}
