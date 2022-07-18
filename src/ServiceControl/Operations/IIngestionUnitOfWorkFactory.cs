namespace ServiceControl.Operations
{
    interface IIngestionUnitOfWorkFactory
    {
        IIngestionUnitOfWork StartNew();
    }
}