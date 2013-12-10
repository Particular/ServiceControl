namespace ServiceControl.Operations
{
    using Contracts.Operations;
    using NServiceBus;

    public abstract class ImportEnricher : IEnrichImportedMessages
    {
        public abstract void Enrich(ImportMessage message);

       
    }
    interface IEnrichImportedMessages
    {
        void Enrich(ImportMessage message);

    }

    class ImportRegistration:INeedInitialization
    {
        public void Init()
        {
            Configure.Instance.ForAllTypes<IEnrichImportedMessages>(
                t => Configure.Component(t, DependencyLifecycle.InstancePerCall));
        
        }
    }
}