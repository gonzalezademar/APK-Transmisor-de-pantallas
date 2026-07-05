using System;

namespace DroneScreenViewer.Models
{
    public class MediaRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FlightSessionId { get; set; } = string.Empty;
        
        // Ruta del archivo en el disco
        public string FilePath { get; set; } = string.Empty;
        
        // "Photo" o "Video"
        public string MediaType { get; set; } = "Photo"; 
        
        public DateTime CaptureTime { get; set; } = DateTime.Now;
        
        // Metadatos de Inspección (Se llenan desde el panel emergente)
        public string ElementType { get; set; } = string.Empty;
        public string AnomalyType { get; set; } = string.Empty;
        public string Criticality { get; set; } = string.Empty;
        public string Observations { get; set; } = string.Empty;
    }
}
