// Wraps WIA camera enumeration and provides helper methods for safer access and formatting.

using System;
using System.Collections.Generic;
using System.Management;
using Emgu.CV;

namespace winbooth.Services
{
    public static class CameraHelper
    {
        public static List<string> GetAllCameraNames()
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            static bool ContainsKeyword(string source)
            {
                if (string.IsNullOrWhiteSpace(source))
                    return false;

                var lower = source.ToLowerInvariant();
                return lower.Contains("camera") ||
                       lower.Contains("imaging") ||
                       lower.Contains("video") ||
                       lower.Contains("webcam");
            }

            const string ImagingGuid = "{6BDD1FC6-810F-11D0-BEC7-08002BE2092F}";

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Caption, Description, PNPClass, ClassGuid, Service FROM Win32_PnPEntity WHERE " +
                    "(Description LIKE '%Camera%' OR " +
                    "Description LIKE '%Imaging%' OR " +
                    "Description LIKE '%Webcam%' OR " +
                    "Caption LIKE '%Camera%' OR " +
                    "Caption LIKE '%Imaging%' OR " +
                    "Caption LIKE '%Webcam%' OR " +
                    "Caption LIKE '%Video%' OR " +
                    "PNPClass = 'Camera' OR " +
                    "PNPClass = 'Image' OR " +
                    "Service = 'usbvideo' OR " +
                    "ClassGuid = '{6BDD1FC6-810F-11D0-BEC7-08002BE2092F}')"))
                {
                    foreach (var device in searcher.Get())
                    {
                        string caption = device["Caption"]?.ToString();
                        string description = device["Description"]?.ToString();
                        string pnpClass = device["PNPClass"]?.ToString();
                        string classGuid = device["ClassGuid"]?.ToString();
                        string service = device["Service"]?.ToString();

                        bool matchesKeyword = ContainsKeyword(caption) || ContainsKeyword(description);
                        bool matchesClass = string.Equals(pnpClass, "Camera", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(pnpClass, "Image", StringComparison.OrdinalIgnoreCase);
                        bool matchesGuid = string.Equals(classGuid, ImagingGuid, StringComparison.OrdinalIgnoreCase);
                        bool matchesService = string.Equals(service, "usbvideo", StringComparison.OrdinalIgnoreCase);

                        if (matchesKeyword || matchesClass || matchesGuid || matchesService)
                        {
                            string name = caption ?? description ?? "Unbekanntes Gerät";
                            if (seen.Add(name))
                                names.Add(name);
                        }
                    }
                }
            }
            catch { /* Ignore WMI enumeration failures */ }

            // Fallback: probe whether a VideoCapture(0) source is available.
            if (names.Count == 0)
            {
                try
                {
                    using (var cap = new VideoCapture(0))
                    {
                        if (cap.IsOpened)
                        {
                            const string fallback = "Standard-Kamera (Video0)";
                            if (seen.Add(fallback))
                                names.Add(fallback);
                        }
                    }
                }
                catch { /* Ignore VideoCapture fallback failures */ }
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
