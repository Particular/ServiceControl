namespace ServiceControl.Operations
{
    using System.Threading.Tasks;

    interface IIngestionUnitOfWorkFactory
    {
        ValueTask<IIngestionUnitOfWork> StartNew();
    }
}