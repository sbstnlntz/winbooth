using System.Collections.Generic;
using System.Drawing.Printing;

namespace FotoboxApp.Services
{
	public static class PrinterHelper
	{
		public static string GetDefaultPrinterName()
		{
			try
			{
				PrinterSettings settings = new PrinterSettings();
				return settings.PrinterName ?? "Kein Drucker gefunden";
			}
			catch
			{
				return "Kein Drucker gefunden";
			}
		}

		// Gibt alle installierten Druckernamen zurück
		public static List<string> GetAllPrinterNames()
		{
			var names = new List<string>();
			foreach (string printer in PrinterSettings.InstalledPrinters)
			{
				names.Add(printer);
			}

			// Sicherstellen, dass es mindestens einen Eintrag gibt
			if (names.Count == 0)
				names.Add("Kein Drucker gefunden");

			return names;
		}
	}
}
