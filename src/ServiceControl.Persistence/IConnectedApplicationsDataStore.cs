namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IConnectedApplicationsDataStore
    {
        Task Add(string connectedApplication);
        Task<IList<string>> GetConnectedApplications();
    }
}