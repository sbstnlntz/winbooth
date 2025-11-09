# winbooth

`winbooth` is a Windows-only WPF application that powers a self‑contained photo booth experience.  
It combines a touchscreen workflow, live camera preview powered by Emgu CV/OpenCV, flexible template
packages, USB/export tooling, and an admin back office into a single executable so events can run
completely offline.

## Key capabilities
- **Guided capture pipeline** – Start screens route guests through template selection, live preview,
  countdown, collage rendering, optional live gallery display, and printing (`Views/CameraView*`,
  `Views/CollageView*`, `Views/LiveGalerieView*`).
- **Template packages** – Admins import `.zip` bundles that contain a `template.xml` description and
  overlay artwork. Templates can be whitelisted per event, previewed, and assigned to two user slots
  (`Services/TemplateStorageService.cs`, `ViewModels/StartViewModel*`).
- **Hardware governance** – Camera and printer lists are pulled via WMI/.NET so only approved devices
  are exposed to the UI (`Services/CameraHelper.cs`, `Services/PrinterHelper.cs`).
- **Event persistence** – Settings, counters, and session state are stored on disk, making the box
  recoverable after power loss and enabling statistics dashboards (`Services/SettingsService.cs`,
  `Utilities/_StatManager.cs`).
- **Backup & restore** – One click archives any gallery folder to `Backups/` under the app directory,
  can be copied to USB, and restored elsewhere (`Services/BackupService.cs`, `Views/AdminMenuView*`).

## Repository layout
- `Views/` – All XAML screens (start/user/admin menus, dialogs, capture and gallery surfaces).
- `ViewModels/` – Presentation logic orchestrating cameras, templates, USB jobs, and stats.
- `Services/` – Long‑running helpers (storage, backup, overlay rendering, diagnostics, device helpers).
- `Models/` – DTOs for templates and import results.
- `Utilities/` – Shared infrastructure (statistics persistence, converters, relay commands).
- `Assets/` – Icons, logos, and default template imagery embedded through `App.xaml`.

## Requirements
- Windows 10/11 with a camera, optional printer, and (for template import) keyboard/mouse access.
- [.NET SDK 8.0](https://dotnet.microsoft.com/download) and Visual Studio 2022 17.8+ (or `dotnet` CLI).
- Emgu.CV native dependencies are restored via NuGet (`winbooth.csproj`), so build on x64 hardware.

## Getting started
1. **Restore packages**
   ```powershell
   dotnet restore winbooth.sln
   ```
2. **Build**
   ```powershell
   dotnet build winbooth.csproj -c Release
   ```
   Visual Studio users can simply open `winbooth.sln` and build the `WinExe` target.
3. **Run**
   ```powershell
   dotnet run --project winbooth.csproj
   ```
   The splash screen will appear, followed by the start view. Attach a USB camera/printer before launch
   to ensure they show up in the device lists.

### Runtime folders
| Location | Purpose |
| --- | --- |
| `templates/` (next to the executable) | User-imported template packages managed by `TemplateStorageService`. |
| `default_templates/` | Factory templates that can be promoted into events. |
| `AppData/config/settings.json` | Persisted admin toggles, gallery name, allowed hardware, template selection. |
| `AppData/metrics/stats.json` | Counters for shots, collages, and prints (`StatManager`). |
| `Backups/` | ZIP archives created through the admin screen. |
| `%UserProfile%\Pictures\Fotobox\<Gallery>\` | All captured JPGs, session `shots/` folders, and live-gallery assets. |

## Template packages
Templates are regular `.zip` files. When the user uploads them through the UI (or copies them into
`templates/`), the application extracts the package and reads `template.xml` with `TemplateLoader`.

Minimum structure:
```
MyTemplate.zip
├─ template.xml              # defines canvas size and <Photo> regions
├─ overlay.png               # referenced via the ImagePath attribute
└─ assets/...                # any additional artwork/fonts you use
```

A simplified `template.xml` looks like:
```xml
<Template Width="1800" Height="1200">
  <Elements>
    <Image ImagePath="overlay.png" />
    <Photo Left="120" Top="140" Width="520" Height="700" Rotation="0" />
    <Photo Left="940" Top="140" Width="520" Height="700" Rotation="0" />
  </Elements>
</Template>
```
Photos are filled in the order determined by `PhotoNumber`/`Name` attributes (see
`Services/TemplateLoader.cs` for the parsing rules). The overlay is composited last by
`Views/CameraView.xaml.cs`, so transparent PNGs work best.

## Galleries, live view, and backups
- Each gallery/event you create in the admin menu maps to `%Pictures%\Fotobox\<GalleryName>`.
  `CameraView` writes raw shots to `shots/<timestamp>` folders while `CollageView` saves the final JPGs
  into the gallery root for printing and the live gallery.
- `Views/LiveGalerieView.xaml` reads the JPGs back and shows them on a secondary screen. Keeping the
  gallery folder on SSD dramatically improves loading speed.
- Use the admin backup overlay to archive any gallery. ZIPs land in `Backups/` under the install folder
  and can be restored to any path (including USB drives) through the same screen.

## Development notes
- `StartViewModel` is the central coordinator: it multiplexes long‑running tasks, monitors hardware
  heartbeats, queues USB copy jobs, and exposes the state consumed across views.
- Settings and stats are persisted asynchronously; if you tweak either service, run the app once so the
  new schema is written to `AppData`.
- Diagnostics live in `Services/DiagnosticsLogger.cs`; hook into it when adding new background workers so
  watchdogs such as the camera heartbeat can surface meaningful errors.

## License
This project is distributed under the proprietary terms in [`LICENSE`](LICENSE). Review it before
sharing binaries or assets.
