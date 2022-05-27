namespace ServiceControl.CustomChecks
{
    using EventLog;

    class CustomCheckDeletedDefinition : EventLogMappingDefinition<CustomCheckDeleted>
    {
        public CustomCheckDeletedDefinition()
        {
            Description(m => "Custom check muted.");

            RelatesToCustomCheck(c => c.Id.ToString());
        }
    }
}