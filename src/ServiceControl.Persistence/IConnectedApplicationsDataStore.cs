namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IConnectedApplicationsDataStore
    {
        Task AddIfNotExists(string connectedApplication);
        Task<IList<string>> GetConnectedApplications();
    }
}