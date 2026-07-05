using System;

namespace DroneScreenViewer.Models
{
    public class FlightSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PilotName { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public string Observations { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
        
        // Este será el nombre de la carpeta en el disco duro
        public string SessionFolderName => $"VUELO_{StartTime:yyyyMMdd_HHmm}_{PilotName.Replace(" ", "")}";
    }
}
