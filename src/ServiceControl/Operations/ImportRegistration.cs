namespace ServiceControl.Operations
{
    using NServiceBus;

    class ImportRegistration:INeedInitialization
    {
        public void Init()
        {
            Configure.Instance.ForAllTypes<IEnrichImportedMessages>(
                t => Configure.Component(t, DependencyLifecycle.SingleInstance));
        
        }
    }
}