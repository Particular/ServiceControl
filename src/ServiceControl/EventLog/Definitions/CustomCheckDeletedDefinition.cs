namespace ServiceControl.EventLog.Definitions
{
    using CustomChecks;


    class CustomCheckDeletedDefinition : EventLogMappingDefinition<CustomCheckDeleted>
    {
        public CustomCheckDeletedDefinition()
        {
            Description(m => "Custom check muted.");

            RelatesToCustomCheck(c => c.Id.ToString());
        }
    }
}