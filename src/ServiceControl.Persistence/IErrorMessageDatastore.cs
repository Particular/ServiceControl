namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;

    public interface IErrorMessageDataStore
    {
        Task<Infrastructure.QueryResult<IList<Persistence.RavenDb.MessagesView>>> GetAllMessages(HttpRequestMessage request);
    }
}
