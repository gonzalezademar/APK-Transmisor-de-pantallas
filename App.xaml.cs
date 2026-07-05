using System;
using System.Threading;
using System.Windows;

namespace DroneScreenViewer
{
    public partial class App : Application
    {
        private static Mutex _mutex;
        private const string MutexName = "Global\\DroneScreenViewer_App_Mutex_v1";

        protected override void OnStartup(StartupEventArgs e)
        {
            // Implementación del Mutex para prevenir múltiples instancias (Cero Zombies)
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            
            if (!createdNew)
            {
                // Otra instancia ya está corriendo. Cerramos esta silenciosamente.
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch { /* Ignoramos si el hilo ya no posee el mutex */ }
                
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
