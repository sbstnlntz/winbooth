using System.Collections.ObjectModel;
using System.Windows;

namespace FotoboxApp.Views
{
    public partial class CameraSelectDialog : Window
    {
        public string SelectedCamera { get; private set; }

        public CameraSelectDialog(ObservableCollection<string> cameras, string currentCamera)
        {
            InitializeComponent();
            ListBoxCameras.ItemsSource = cameras;
            if (!string.IsNullOrEmpty(currentCamera))
                ListBoxCameras.SelectedItem = currentCamera;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (ListBoxCameras.SelectedItem != null)
            {
                SelectedCamera = ListBoxCameras.SelectedItem.ToString();
                this.DialogResult = true;
            }
        }
    }
}
