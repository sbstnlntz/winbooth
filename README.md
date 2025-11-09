# WinBooth

WinBooth is a Windows-only WPF application that turns a PC with a connected camera, printer, and USB storage into a fully managed photo booth. It coordinates camera capture, template-based layouting, printing, archiving, and gallery export while providing an operator-friendly admin surface for live events.

## Highlights

- **Guest-first start screen** with template previews, countdown status, and quick admin entry.
- **Admin dashboard** to manage cameras, printers, USB drives, countdown timings, and live gallery exports without leaving the kiosk mode UI.
- **Template, collage, and graphics workflow** that lets you assign default templates, multi-select template packs, and update instructional graphics on the fly.
- **Hardware health monitoring** surfaces sensor, storage, and statistics issues in real time so staff can react before sessions fail.
- **Media pipeline** powered by Emgu CV for camera access, live filters (e.g., black/white), and post-capture rendering.
- **Persistent storage helpers** for automatic backups, gallery exports, and customer design packages.

## Technology Stack

- .NET 8.0 (Windows) with WPF and a custom theme (`Themes/Styles.xaml`).
- Emgu CV 4.11 for camera/video frames and image processing.
- MVVM-style organization (`ViewModels`, `Views`, `Services`, `Utilities`).
- Declarative resources and assets stored under `Assets/` (PNG resources are embedded automatically via `winbooth.csproj`).

## Repository Layout

| Path | Description |
| --- | --- |
| `App.xaml` + `.cs` | Application bootstrapper, resource registration, splash screen sequence. |
| `MainWindow.xaml` | Root window hosting the StartView and navigation frame (borderless, kiosk layout). |
| `Views/` | XAML screens and dialogs (Start, AdminMenu, CameraSelectDialog, Template pickers, etc.). |
| `ViewModels/` | Start view orchestration split across partial classes for templates, graphics, allowed devices, etc. |
| `Services/` | Device discovery, storage, printing, and persistence helpers. |
| `Models/` | DTOs for templates, devices, gallery entries, configuration snapshots. |
| `Assets/` | Embedded PNG resources such as `logo.png`, default graphics, overlays. |
| `Utilities/` | Cross-cutting helpers (file dialogs, threading, conversion utilities). |

## Prerequisites

1. **Windows 10/11 x64** with desktop experience (WPF is Windows-only).
2. **.NET 8.0 SDK** (`dotnet --list-sdks` should list `8.0.x`).  
3. **Camera/Printer drivers** installed and visible to Windows.  
4. **Visual C++ Runtime** (installed automatically by Visual Studio; required by Emgu CV native binaries).  
5. Optional: **Visual Studio 2022** or **VS Code + C# Dev Kit** for a richer editing/debugging workflow.

## Getting Started

```bash
git clone <repository-url>
cd FotoboxApp
dotnet restore
```

### Build & Run

```bash
dotnet build            # Compiles the WPF application
dotnet run --project winbooth.csproj
```

Or open `winbooth.sln` inside Visual Studio, set `winbooth` as the startup project, and press `F5` to launch with the debugger.

## Configuration Notes

- **Application icon:** `MainWindow.xaml` sets `Icon="Assets/logo.png"`, so replacing `Assets/logo.png` changes both the window chrome and the taskbar icon. Ensure the file remains a square PNG and keep its `Build Action` as `Resource` (default).
- **Themes and brushes:** adjust colors and fonts inside `Themes/Styles.xaml` or `App.xaml` resource dictionaries.
- **Templates & graphics:** template ZIP packages live next to the executable inside the `templates/` folder (user-managed) and `default_templates/` (fallback bundles). The Admin UI copies imported packs there. Only built-in PNGs (logos, overlays) stay in `Assets/`.
- **Backups and galleries:** the admin menu exposes controls for exporting backups and galleries. The corresponding logic lives in `Views/AdminMenuView.xaml(.cs)` and `ViewModels/StartViewModel.*` for reference if you need to script or automate exports.

## Troubleshooting

- **Camera not detected:** verify the camera appears in Windows, then check the Admin -> Devices panel. Logs in `ViewModels/StartViewModel.AllowedDevices.cs` describe how summaries are built.
- **Printer queue issues:** ensure the printer driver is installed; use the Admin Printer dialog to select the device again so WinBooth refreshes its cached name.
- **Template persistence errors:** exceptions bubble up with user-facing strings inside `StartViewModel.Templates.cs`. Inspect those sections to add logging or diagnostics.
- **Native library load failures:** confirm `Emgu.CV.runtime.windows` copied its native DLLs to `bin/Debug/net8.0-windows`. Missing VC++ runtime or antivirus quarantining files are the most common causes.

## Deployment

Generate a self-contained build for distribution:

```bash
dotnet publish winbooth.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Copy the resulting `publish` folder to the kiosk machine together with your template/assets directories.

## Contributing & Next Steps

- Follow the existing MVVM pattern when adding screens - new dialogs live in `Views/`, with backing logic in `ViewModels/` or specialized services.
- Keep shipped UI artwork under `Assets/` so it is embedded, but place distributable template packs in the runtime `templates/` and `default_templates/` folders managed by `TemplateStorageService`.
- Consider adding automated UI or integration tests if you extend the pipeline; `dotnet test` will pick up any future test projects.

Happy capturing!
