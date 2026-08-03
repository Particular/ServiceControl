namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Persistence.MessageRedirects;

public class MessageRedirectsDataStore : IMessageRedirectsDataStore
{
    public Task<IReadOnlyList<MessageRedirect>> GetRedirects() =>
        throw new NotImplementedException();

    public Task AddRedirect(MessageRedirect redirect) =>
        throw new NotImplementedException();

    public Task UpdateRedirect(MessageRedirect redirect) =>
        throw new NotImplementedException();

    public Task RemoveRedirect(MessageRedirect redirect) =>
        throw new NotImplementedException();
}
