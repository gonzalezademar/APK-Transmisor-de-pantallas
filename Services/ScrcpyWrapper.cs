using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DroneScreenViewer.Services
{
    public class ScrcpyWrapper : IDisposable
    {
        private Process _process;
        private readonly string _scrcpyPath;
        private bool _isDisposed;

        // Evento para notificar a la UI si el proceso muere externamente
        public event EventHandler ProcessExited;

        public ScrcpyWrapper()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _scrcpyPath = Path.Combine(baseDir, "Resources", "scrcpy-win64", "scrcpy.exe");
            KillOrphanedInstances();
        }

        public Process ScrcpyProcess => _process;
        public bool IsRunning => _process != null && !_process.HasExited;

        public void KillOrphanedInstances()
        {
            try
            {
                var processes = Process.GetProcessesByName("scrcpy");
                foreach (var p in processes)
                {
                    try { p.Kill(); } 
                    catch { /* Estimado: Fallo silencioso si no hay permisos o ya cerró */ }
                }
            }
            catch { /* Pendiente de validar: Sistemas con políticas de seguridad estrictas (WMI) */ }
        }

        public async Task StartStreamingAsync()
        {
            if (IsRunning) return;
            
            if (!File.Exists(_scrcpyPath)) 
                throw new FileNotFoundException("El binario scrcpy.exe no fue encontrado en la ruta esperada.");

            KillOrphanedInstances();

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _scrcpyPath,
                    Arguments = "--window-title \"Drone Viewer\" --max-fps=60 --video-bit-rate=16M --no-audio",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _process.EnableRaisingEvents = true;
            _process.Exited += (s, e) => ProcessExited?.Invoke(this, EventArgs.Empty);

            bool started = _process.Start();
            if (!started)
            {
                throw new InvalidOperationException("El sistema operativo rechazó el inicio del proceso scrcpy.");
            }

            await Task.CompletedTask; // Mantiene la firma asíncrona para expansión futura
        }

        public void StopStreaming()
        {
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    _process.Kill();
                    _process.WaitForExit(1000); // Timeout de seguridad (1s)
                }
                catch { /* Estimado: Si falla, el proceso ya está en estado zombie/terminado */ }
            }

            if (_process != null)
            {
                _process.Dispose();
                _process = null;
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                StopStreaming();
                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
