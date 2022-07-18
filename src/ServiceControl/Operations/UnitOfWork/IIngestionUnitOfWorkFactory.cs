namespace ServiceControl.Operations
{
    using System.Threading.Tasks;

    interface IIngestionUnitOfWorkFactory
    {
        Task<IIngestionUnitOfWork> StartNew();
    }
}