namespace ServiceControl.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IConnectedApplicationsDataStore
    {
        Task<ConnectedApplication[]> GetAllConnectedApplications();

        Task UpdateConnectedApplication(ConnectedApplication connectedApplication, CancellationToken token);
    }
}