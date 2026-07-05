using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Dapper;

namespace DroneScreenViewer.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public DatabaseService()
        {
            // La base de datos vivirá en Mis Documentos/AssetGuardian para que no se borre si reinstalas
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appFolder = Path.Combine(docPath, "AssetGuardian");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _dbPath = Path.Combine(appFolder, "AssetGuardian.db");
            _connectionString = $"Data Source={_dbPath}";
        }

        public async Task InitializeDatabaseAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            string createFlightsTable = @"
                CREATE TABLE IF NOT EXISTS FlightSessions (
                    Id TEXT PRIMARY KEY,
                    PilotName TEXT NOT NULL,
                    TaskType TEXT NOT NULL,
                    Location TEXT NOT NULL,
                    Stage TEXT NOT NULL,
                    Observations TEXT,
                    StartTime DATETIME NOT NULL,
                    EndTime DATETIME
                );";

            string createMediaTable = @"
                CREATE TABLE IF NOT EXISTS MediaRecords (
                    Id TEXT PRIMARY KEY,
                    FlightSessionId TEXT NOT NULL,
                    FilePath TEXT NOT NULL,
                    MediaType TEXT NOT NULL,
                    CaptureTime DATETIME NOT NULL,
                    ElementType TEXT,
                    AnomalyType TEXT,
                    Criticality TEXT,
                    Observations TEXT,
                    FOREIGN KEY(FlightSessionId) REFERENCES FlightSessions(Id)
                );";

            await connection.ExecuteAsync(createFlightsTable);
            await connection.ExecuteAsync(createMediaTable);
        }

        public async Task InsertFlightSessionAsync(Models.FlightSession session)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = @"
                INSERT INTO FlightSessions (Id, PilotName, TaskType, Location, Stage, Observations, StartTime) 
                VALUES (@Id, @PilotName, @TaskType, @Location, @Stage, @Observations, @StartTime);";
            
            await connection.ExecuteAsync(sql, session);
        }

        public async Task UpdateFlightSessionEndTimeAsync(string id, DateTime endTime)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = "UPDATE FlightSessions SET EndTime = @EndTime WHERE Id = @Id;";
            await connection.ExecuteAsync(sql, new { Id = id, EndTime = endTime });
        }

        public async Task InsertMediaRecordAsync(Models.MediaRecord record)
        {
            using var connection = new SqliteConnection(_connectionString);
            string sql = @"
                INSERT INTO MediaRecords (Id, FlightSessionId, FilePath, MediaType, CaptureTime, ElementType, AnomalyType, Criticality, Observations) 
                VALUES (@Id, @FlightSessionId, @FilePath, @MediaType, @CaptureTime, @ElementType, @AnomalyType, @Criticality, @Observations);";
            
            await connection.ExecuteAsync(sql, record);
        }
    }
}
