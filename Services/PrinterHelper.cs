// Utility for enumerating printers, resolving driver info, and triggering diagnostics prints.

using System.Collections.Generic;
using System.Drawing.Printing;

namespace winbooth.Services
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

		// Returns all installed printer names.
		public static List<string> GetAllPrinterNames()
		{
			var names = new List<string>();
			foreach (string printer in PrinterSettings.InstalledPrinters)
			{
				names.Add(printer);
			}

			// Ensure at least one entry is returned.
			if (names.Count == 0)
				names.Add("Kein Drucker gefunden");

			return names;
		}
	}
}
