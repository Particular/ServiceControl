namespace ServiceControl.Persistence.Sql;

using System.Threading.Tasks;
using ServiceControl.Persistence.MessageRedirects;

class NoOpMessageRedirectsDataStore : IMessageRedirectsDataStore
{
    public Task<MessageRedirectsCollection> GetOrCreate() =>
        Task.FromResult(new MessageRedirectsCollection());

    public Task Save(MessageRedirectsCollection redirects) => Task.CompletedTask;
}
