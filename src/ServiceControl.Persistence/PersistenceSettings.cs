namespace ServiceControl.Persistence
{
    /// <summary>
    /// Marker interface used to serialize persister settings in REST API
    /// </summary>
    public abstract class PersistenceSettings
    {
        public bool MaintenanceMode { get; set; }
    }
}