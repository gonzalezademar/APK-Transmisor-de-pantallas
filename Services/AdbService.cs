using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DroneScreenViewer.Models;

namespace DroneScreenViewer.Services
{
    public class AdbService
    {
        private readonly string _adbPath;

        public AdbService()
        {
            // Busca adb.exe en la carpeta Resources/scrcpy-win64
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _adbPath = Path.Combine(baseDir, "Resources", "scrcpy-win64", "adb.exe");
        }

        public async Task<AdbDeviceState> GetDeviceStateAsync()
        {
            if (!File.Exists(_adbPath))
            {
                return AdbDeviceState.Error;
            }

            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _adbPath,
                        Arguments = "devices",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return ParseAdbOutput(output);
            }
            catch
            {
                return AdbDeviceState.Error;
            }
        }

        public async Task RestartAdbServerAsync()
        {
            if (!File.Exists(_adbPath)) return;

            try
            {
                using var pKill = Process.Start(new ProcessStartInfo { FileName = _adbPath, Arguments = "kill-server", CreateNoWindow = true, UseShellExecute = false });
                if (pKill != null) await pKill.WaitForExitAsync();

                using var pStart = Process.Start(new ProcessStartInfo { FileName = _adbPath, Arguments = "start-server", CreateNoWindow = true, UseShellExecute = false });
                if (pStart != null) await pStart.WaitForExitAsync();
            }
            catch { /* Fallo silencioso: el próximo polling recuperará el estado automáticamente */ }
        }

        private AdbDeviceState ParseAdbOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return AdbDeviceState.NoDevice;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            AdbDeviceState state = AdbDeviceState.NoDevice;

            // La primera línea suele ser "List of devices attached", la saltamos.
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim().ToLower();
                
                if (line.EndsWith("unauthorized"))
                {
                    return AdbDeviceState.Unauthorized;
                }
                else if (line.EndsWith("offline"))
                {
                    return AdbDeviceState.Offline;
                }
                else if (line.EndsWith("device"))
                {
                    return AdbDeviceState.Connected;
                }
            }

            return state;
        }
    }
}
