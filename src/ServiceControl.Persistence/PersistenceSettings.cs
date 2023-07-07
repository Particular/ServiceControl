namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;

    public class PersistenceSettings
    {
        public PersistenceSettings()
        {
            PersisterSpecificSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public bool MaintenanceMode { get; set; }
        public IDictionary<string, string> PersisterSpecificSettings { get; }
    }
}
