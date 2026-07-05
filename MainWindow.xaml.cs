using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DroneScreenViewer.Models;
using DroneScreenViewer.Services;
using DroneScreenViewer.Views;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Runtime.InteropServices;
using System.Windows.Forms.Integration;
namespace DroneScreenViewer
{
    public partial class MainWindow : System.Windows.Window, IDisposable
    {
        private FlightSession? _activeSession = null;
        private readonly DatabaseService _dbService;
        
        // Servicios de Video OpenCV
        private VideoCapture? _videoCapture;
        private Mat? _currentFrame;
        private DispatcherTimer? _videoTimer;
        
        // Servicios Legacy (ADB)
        private readonly AdbService _adbService;
        private readonly ScrcpyWrapper _scrcpyWrapper;
        private readonly DispatcherTimer _monitorTimer;
        private AdbDeviceState _currentDeviceState = AdbDeviceState.NoDevice;
        private bool _isDisposed = false;

        // APIs de Windows para capturar la ventana externa
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        private const int GWL_STYLE = -16;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_CHILD = 0x40000000;
        
        private WindowsFormsHost? _scrcpyHost;
        private System.Windows.Forms.Panel? _scrcpyPanel;

        public MainWindow()
        {
            InitializeComponent();
            
            // Inicializar base de datos
            _dbService = new DatabaseService();
            _ = _dbService.InitializeDatabaseAsync();

            // Inicializar servicios ADB (Para retrocompatibilidad)
            _adbService = new AdbService();
            _scrcpyWrapper = new ScrcpyWrapper();
            
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _monitorTimer.Tick += async (s, e) => 
            {
                if (CmbVideoSource.SelectedIndex == 1) // Si ADB está seleccionado
                {
                    _currentDeviceState = await _adbService.GetDeviceStateAsync();
                    UpdateAdbUI();
                }
            };
            _monitorTimer.Start();
        }

        private async void BtnToggleSession_Click(object sender, RoutedEventArgs e)
        {
            if (_activeSession == null)
            {
                // Modo Bloqueado -> Intentar Desbloquear (Iniciar Misión)
                var sessionWindow = new FlightSessionWindow();
                sessionWindow.Owner = this;
                
                if (sessionWindow.ShowDialog() == true)
                {
                    _activeSession = sessionWindow.CreatedSession;
                    
                    // Guardar en Base de Datos
                    await _dbService.InsertFlightSessionAsync(_activeSession);
                    
                    // Actualizar UI a Modo Desbloqueado
                    LockStatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#103320"));
                    LockStatusText.Text = "SESIÓN ACTIVA";
                    LockStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    
                    BtnToggleSession.Content = "FINALIZAR MISIÓN";
                    BtnToggleSession.Style = (Style)FindResource("DangerButton");

                    PanelFlightData.Visibility = Visibility.Visible;
                    TxtInfoPilot.Text = _activeSession.PilotName;
                    TxtInfoLocation.Text = _activeSession.Location;
                    TxtInfoFolder.Text = _activeSession.SessionFolderName;

                    // Habilitar herramientas de captura
                    BtnCaptureImage.IsEnabled = true;
                    BtnRecordVideo.IsEnabled = true;
                    BtnStartAdbVideo.Visibility = Visibility.Visible;
                    
                    // Iniciar captura UVC
                    StartCameraStream();
                }
            }
            else
            {
                // Modo Desbloqueado -> Bloquear (Finalizar Misión)
                var result = MessageBox.Show("¿Estás seguro de finalizar esta misión? Se cerrará el registro de vuelo.", "Finalizar Misión", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    if (_activeSession != null)
                    {
                        await _dbService.UpdateFlightSessionEndTimeAsync(_activeSession.Id, DateTime.Now);
                    }

                    _activeSession = null;
                    
                    // Restaurar UI a Modo Bloqueado
                    LockStatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#312222"));
                    LockStatusText.Text = "SISTEMA BLOQUEADO";
                    LockStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    
                    BtnToggleSession.Content = "INICIAR MISIÓN DE VUELO";
                    BtnToggleSession.Style = (Style)FindResource("ActionButton");

                    PanelFlightData.Visibility = Visibility.Collapsed;

                    // Deshabilitar herramientas
                    BtnCaptureImage.IsEnabled = false;
                    BtnRecordVideo.IsEnabled = false;
                    BtnStartAdbVideo.Visibility = Visibility.Collapsed;
                    
                    // Detener grabación/video si estuviera activo
                    _scrcpyWrapper.StopStreaming();
                    StopCameraStream();
                }
            }
        }

        private void UpdateAdbUI()
        {
            Dispatcher.Invoke(() =>
            {
                switch (_currentDeviceState)
                {
                    case AdbDeviceState.NoDevice:
                        AdbStatusText.Text = "ADB: SIN DISPOSITIVO";
                        break;
                    case AdbDeviceState.Connected:
                        AdbStatusText.Text = "ADB: CONECTADO";
                        break;
                }
            });
        }

        private async void BtnStartAdbVideo_Click(object sender, RoutedEventArgs e)
        {
            if (_activeSession == null) return;
            
            try
            {
                BtnStartAdbVideo.IsEnabled = false;
                BtnStartAdbVideo.Content = "CONECTANDO...";
                
                await _scrcpyWrapper.StartStreamingAsync();
                
                // Esperar a que Scrcpy cree su ventana principal
                IntPtr scrcpyHwnd = IntPtr.Zero;
                for (int i = 0; i < 20; i++) // Intentar por 10 segundos
                {
                    if (_scrcpyWrapper.ScrcpyProcess != null)
                    {
                        _scrcpyWrapper.ScrcpyProcess.Refresh();
                        if (_scrcpyWrapper.ScrcpyProcess.MainWindowHandle != IntPtr.Zero)
                        {
                            scrcpyHwnd = _scrcpyWrapper.ScrcpyProcess.MainWindowHandle;
                            break;
                        }
                    }
                    await Task.Delay(500);
                }

                if (scrcpyHwnd != IntPtr.Zero)
                {
                    // Ocultar texto y boton
                    TxtNoSignal.Visibility = Visibility.Collapsed;
                    BtnStartAdbVideo.Visibility = Visibility.Collapsed;
                    
                    // Crear el contenedor si no existe
                    if (_scrcpyHost == null)
                    {
                        _scrcpyHost = new WindowsFormsHost();
                        _scrcpyPanel = new System.Windows.Forms.Panel();
                        _scrcpyPanel.BackColor = System.Drawing.Color.Black;
                        _scrcpyHost.Child = _scrcpyPanel;
                        VideoContainer.Children.Add(_scrcpyHost);
                        
                        // Evento de redimensionamiento
                        _scrcpyPanel.Resize += (s, ev) => 
                        {
                            if (_scrcpyWrapper.IsRunning && _scrcpyWrapper.ScrcpyProcess.MainWindowHandle != IntPtr.Zero)
                            {
                                MoveWindow(_scrcpyWrapper.ScrcpyProcess.MainWindowHandle, 0, 0, _scrcpyPanel.Width, _scrcpyPanel.Height, true);
                            }
                        };
                    }

                    _scrcpyHost.Visibility = Visibility.Visible;

                    // Convertir la ventana de Scrcpy en un control "hijo" (sin bordes) incrustado en el Panel
                    SetWindowLong(scrcpyHwnd, GWL_STYLE, WS_VISIBLE | WS_CHILD);
                    SetParent(scrcpyHwnd, _scrcpyPanel.Handle);
                    
                    // Ajustar tamaño inicial
                    MoveWindow(scrcpyHwnd, 0, 0, _scrcpyPanel.Width, _scrcpyPanel.Height, true);
                }
                else
                {
                    MessageBox.Show("Se inició Scrcpy pero no se pudo capturar su ventana.");
                    BtnStartAdbVideo.IsEnabled = true;
                    BtnStartAdbVideo.Content = "ABRIR VENTANA SCRCPY";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fallo al iniciar Scrcpy: {ex.Message}");
                BtnStartAdbVideo.IsEnabled = true;
                BtnStartAdbVideo.Content = "ABRIR VENTANA SCRCPY";
            }
        }

        private void StartCameraStream()
        {
            if (CmbVideoSource.SelectedIndex != 0) return; // Solo si es Capturadora UVC
            
            _videoCapture = new VideoCapture(0); // 0 es la cámara web/capturadora predeterminada
            if (!_videoCapture.IsOpened())
            {
                MessageBox.Show("No se encontró ninguna capturadora de video o cámara conectada.", "Error de Cámara", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TxtNoSignal.Visibility = Visibility.Collapsed;
            _currentFrame = new Mat();

            _videoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) }; // ~30 FPS
            _videoTimer.Tick += (s, e) =>
            {
                if (_videoCapture != null && _videoCapture.IsOpened())
                {
                    _videoCapture.Read(_currentFrame);
                    if (!_currentFrame.Empty())
                    {
                        VideoPreviewImage.Source = _currentFrame.ToWriteableBitmap();
                    }
                }
            };
            _videoTimer.Start();
        }

        private void StopCameraStream()
        {
            _videoTimer?.Stop();
            _videoCapture?.Release();
            VideoPreviewImage.Source = null;
            TxtNoSignal.Visibility = Visibility.Visible;
        }

        private async void BtnCaptureImage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeSession == null || _currentFrame == null || _currentFrame.Empty()) 
            {
                MessageBox.Show("No hay señal de video válida para capturar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Pausar el flujo de video mientras inspecciona
            _videoTimer?.Stop();

            var metadataWindow = new MetadataCaptureWindow(_currentFrame);
            metadataWindow.Owner = this;
            
            if (metadataWindow.ShowDialog() == true)
            {
                var data = metadataWindow.GeneratedMetadata;
                var finalImage = metadataWindow.ProcessedImage;

                // Crear carpeta de la sesión si no existe
                string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string sessionPath = System.IO.Path.Combine(docPath, "AssetGuardian", _activeSession.SessionFolderName);
                if (!System.IO.Directory.Exists(sessionPath))
                    System.IO.Directory.CreateDirectory(sessionPath);

                // Generar nombre de archivo único
                string fileName = $"CAP_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string fullPath = System.IO.Path.Combine(sessionPath, fileName);
                
                // Guardar la imagen con OpenCV
                Cv2.ImWrite(fullPath, finalImage);
                data.FilePath = fullPath;
                data.FlightSessionId = _activeSession.Id;
                
                // Guardar metadatos en Base de Datos
                await _dbService.InsertMediaRecordAsync(data);

                MessageBox.Show($"¡Captura Guardada en la base de datos!\n\nArchivo: {fileName}\nElemento: {data.ElementType}\nAnomalía: {data.AnomalyType}\nCriticidad: {data.Criticality}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            
            // Reanudar video
            _videoTimer?.Start();
        }

        private void BtnRecordVideo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Fase 3: Aquí comenzará a guardarse el archivo MP4 en la carpeta del vuelo.");
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                StopCameraStream();
                _currentFrame?.Dispose();
                
                _monitorTimer?.Stop();
                _scrcpyWrapper?.Dispose();
                _isDisposed = true;
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            Dispose();
            base.OnClosed(e);
        }
    }
}
