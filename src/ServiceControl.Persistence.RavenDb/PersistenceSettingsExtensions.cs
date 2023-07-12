namespace ServiceControl.Persistence
{
    using System;

    public static class PersistenceSettingsExtensions
    {
        public static int ExpirationProcessTimerInSeconds(this PersistenceSettings instance)
        {
            // TODO: Can this item not exist?
            return int.Parse(instance.PersisterSpecificSettings["ExpirationProcessTimerInSeconds"]);
        }

        public static int ExpirationProcessBatchSize(this PersistenceSettings instance)
        {
            return int.Parse(instance.PersisterSpecificSettings["ExpirationProcessBatchSize"]);
        }

        public static int ExternalIntegrationsDispatchingBatchSize(this PersistenceSettings instance)
        {
            return int.Parse(instance.PersisterSpecificSettings["ExternalIntegrationsDispatchingBatchSize"]);
        }
    }
}