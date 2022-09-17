namespace ServiceControl.Audit.Persistence
{
    using System.Collections.Generic;

    public class PersistenceSettings
    {
        public PersistenceSettings(IDictionary<string, string> persisterSpecificSettings)
        {
            PersisterSpecificSettings = persisterSpecificSettings;
        }

        public bool IsSetup { get; set; }
        public bool MaintenanceMode { get; set; }
        public IDictionary<string, string> PersisterSpecificSettings { get; }
    }
}