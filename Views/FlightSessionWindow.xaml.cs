using System.Windows;
using DroneScreenViewer.Models;

namespace DroneScreenViewer.Views
{
    public partial class FlightSessionWindow : Window
    {
        public FlightSession CreatedSession { get; private set; }

        public FlightSessionWindow()
        {
            InitializeComponent();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPilotName.Text) || 
                string.IsNullOrWhiteSpace(TxtTaskType.Text) || 
                string.IsNullOrWhiteSpace(TxtLocation.Text))
            {
                MessageBox.Show("Piloto, Tarea y Locación son campos obligatorios.", "Faltan datos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CreatedSession = new FlightSession
            {
                PilotName = TxtPilotName.Text.Trim(),
                TaskType = TxtTaskType.Text.Trim(),
                Location = TxtLocation.Text.Trim(),
                Stage = TxtStage.Text.Trim(),
                Observations = TxtObservations.Text.Trim()
            };

            DialogResult = true;
            Close();
        }
    }
}
