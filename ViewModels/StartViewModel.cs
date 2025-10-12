using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using FotoboxApp.Models;
using FotoboxApp.Services;

namespace FotoboxApp.ViewModels
{
    public class StartViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public ObservableCollection<TemplateItem> Templates { get; } = new ObservableCollection<TemplateItem>();

        // --- Kameras & Drucker ---
        public ObservableCollection<string> AvailableCameras { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AvailablePrinters { get; } = new ObservableCollection<string>();

        private string _selectedCameraName;
        public string SelectedCameraName
        {
            get => _selectedCameraName;
            set
            {
                if (_selectedCameraName != value)
                {
                    _selectedCameraName = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectedPrinterName;
        public string SelectedPrinterName
        {
            get => _selectedPrinterName;
            set
            {
                if (_selectedPrinterName != value)
                {
                    _selectedPrinterName = value;
                    OnPropertyChanged();
                }
            }
        }

        // --- Template-Auswahl ---
        private TemplateItem _selectedTemplate1;
        public TemplateItem SelectedTemplate1
        {
            get => _selectedTemplate1;
            set
            {
                if (_selectedTemplate1 != value)
                {
                    _selectedTemplate1 = value;
                    OnPropertyChanged();
                    if (ActiveTemplate == null && value != null)
                        ActiveTemplate = value;
                }
            }
        }

        private TemplateItem _selectedTemplate2;
        public TemplateItem SelectedTemplate2
        {
            get => _selectedTemplate2;
            set
            {
                if (_selectedTemplate2 != value)
                {
                    _selectedTemplate2 = value;
                    OnPropertyChanged();
                    if (ActiveTemplate == null && value != null)
                        ActiveTemplate = value;
                }
            }
        }

        private TemplateItem _activeTemplate;
        public TemplateItem ActiveTemplate
        {
            get => _activeTemplate;
            set
            {
                if (_activeTemplate != value)
                {
                    _activeTemplate = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _galleryName = "";
        public string GalleryName
        {
            get => _galleryName;
            set
            {
                if (_galleryName != value)
                {
                    _galleryName = value;
                    OnPropertyChanged();
                    // persist on change
                    try { FotoboxApp.Services.SettingsService.SaveGalleryName(_galleryName); } catch { }
                }
            }
        }

        private int _previewDurationSeconds = 3;
        public int PreviewDurationSeconds
        {
            get => _previewDurationSeconds;
            set
            {
                if (_previewDurationSeconds != value)
                {
                    _previewDurationSeconds = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PreviewDurationMs));
                }
            }
        }
        public int PreviewDurationMs => _previewDurationSeconds * 1000;

        private bool _direktdruck = false;
        public bool Direktdruck
        {
            get => _direktdruck;
            set
            {
                if (_direktdruck != value)
                {
                    _direktdruck = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SaveButtonLabel));
                }
            }
        }

        /// <summary>
        /// Liefert den Text f�r den Speichern-Button:
        /// "NUR SPEICHERN" wenn Direktdruck aktiv ist, sonst "SPEICHERN".
        /// </summary>
        public string SaveButtonLabel => Direktdruck ? "NUR SPEICHERN" : "SPEICHERN";

        private bool _galerieButton = true;
        public bool GalerieButton
        {
            get => _galerieButton;
            set
            {
                if (_galerieButton != value)
                {
                    _galerieButton = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _fotoFilter = false;
        public bool FotoFilter
        {
            get => _fotoFilter;
            set
            {
                if (_fotoFilter != value)
                {
                    _fotoFilter = value;
                    OnPropertyChanged();
                }
            }
        }

        // --- Konstruktor ---
        public StartViewModel()
        {
            // Galerie-Name aus Einstellungen laden
            try { _galleryName = FotoboxApp.Services.SettingsService.LoadGalleryName() ?? ""; } catch { }
            OnPropertyChanged(nameof(GalleryName));

            // Templates laden
            string templatesRoot = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures),
                "Fotobox", "templates"
            );
            if (Directory.Exists(templatesRoot))
            {
                foreach (var zipFile in Directory.GetFiles(templatesRoot, "*.zip"))
                {
                    BitmapImage previewImg = null;
                    try
                    {
                        using (var archive = ZipFile.OpenRead(zipFile))
                        {
                            var entry = archive.GetEntry("preview.png");
                            if (entry != null)
                            {
                                using (var stream = entry.Open())
                                using (var ms = new MemoryStream())
                                {
                                    stream.CopyTo(ms);
                                    ms.Position = 0;
                                    previewImg = new BitmapImage();
                                    previewImg.BeginInit();
                                    previewImg.StreamSource = ms;
                                    previewImg.CacheOption = BitmapCacheOption.OnLoad;
                                    previewImg.EndInit();
                                    previewImg.Freeze();
                                }
                            }
                        }
                    }
                    catch { }

                    Templates.Add(new TemplateItem
                    {
                        Name = Path.GetFileNameWithoutExtension(zipFile),
                        ZipPath = zipFile,
                        PreviewImage = previewImg
                    });
                }
            }

            // --- Kamera- & Druckerliste laden ---
            foreach (var cam in CameraHelper.GetAllCameraNames())
                AvailableCameras.Add(cam);
            foreach (var drucker in PrinterHelper.GetAllPrinterNames())
                AvailablePrinters.Add(drucker);

            // --- Standardauswahl setzen ---
            if (AvailableCameras.Count > 0)
            {
                var stdCam = CameraHelper.GetConnectedCameraName();
                SelectedCameraName = AvailableCameras.Contains(stdCam)
                    ? stdCam
                    : AvailableCameras[0];
            }
            else
            {
                SelectedCameraName = "Keine Kamera gefunden";
            }

            if (AvailablePrinters.Count > 0)
            {
                var stdPrinter = PrinterHelper.GetDefaultPrinterName();
                SelectedPrinterName = AvailablePrinters.Contains(stdPrinter)
                    ? stdPrinter
                    : AvailablePrinters[0];
            }
            else
            {
                SelectedPrinterName = "Kein Drucker gefunden";
            }
        }
    }
}

