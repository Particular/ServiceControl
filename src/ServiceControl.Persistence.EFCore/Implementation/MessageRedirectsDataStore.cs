namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Persistence.MessageRedirects;

public class MessageRedirectsDataStore : IMessageRedirectsDataStore
{
    public Task<MessageRedirectsCollection> GetOrCreate() =>
        throw new NotImplementedException();

    public Task Save(MessageRedirectsCollection redirects) =>
        throw new NotImplementedException();
}
