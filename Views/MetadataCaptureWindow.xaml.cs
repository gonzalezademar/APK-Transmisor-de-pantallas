using System.Windows;
using System.Windows.Controls;
using DroneScreenViewer.Models;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace DroneScreenViewer.Views
{
    public partial class MetadataCaptureWindow : System.Windows.Window
    {
        public MediaRecord GeneratedMetadata { get; private set; }
        public Mat ProcessedImage { get; private set; }
        
        private Mat _originalImage;
        private bool _isInitialized = false;

        public MetadataCaptureWindow(Mat capturedFrame)
        {
            InitializeComponent();
            _originalImage = capturedFrame.Clone();
            ProcessedImage = capturedFrame.Clone();
            
            TxtPreviewPlaceholder.Visibility = Visibility.Collapsed;
            UpdatePreview();
            
            _isInitialized = true;
        }

        private void UpdatePreview()
        {
            if (ProcessedImage != null && !ProcessedImage.Empty())
            {
                ImgPreview.Source = ProcessedImage.ToWriteableBitmap();
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;

            double alpha = SliderContrast.Value;
            double beta = SliderBrightness.Value;

            _originalImage.ConvertTo(ProcessedImage, -1, alpha, beta);
            UpdatePreview();
        }

        private void BtnClahe_Click(object sender, RoutedEventArgs e)
        {
            if (_originalImage.Empty()) return;

            using var labImage = new Mat();
            Cv2.CvtColor(_originalImage, labImage, ColorConversionCodes.BGR2Lab);

            var channels = Cv2.Split(labImage);
            
            using var clahe = Cv2.CreateCLAHE(clipLimit: 2.0, tileGridSize: new OpenCvSharp.Size(8, 8));
            clahe.Apply(channels[0], channels[0]);

            Cv2.Merge(channels, labImage);
            Cv2.CvtColor(labImage, ProcessedImage, ColorConversionCodes.Lab2BGR);
            
            foreach (var ch in channels) ch.Dispose();

            UpdatePreview();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // En una versión final, aquí aplicaríamos los valores de SliderBrightness y SliderContrast
            // a la imagen original usando OpenCV, y guardaríamos el archivo final.

            GeneratedMetadata = new MediaRecord
            {
                ElementType = (CmbElement.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "",
                AnomalyType = (CmbAnomaly.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "",
                Criticality = (CmbCriticality.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "",
                Observations = TxtObservations.Text.Trim(),
                MediaType = "Photo"
            };

            DialogResult = true;
            Close();
        }
    }
}
