namespace ServiceControl.Persistence.MessageRedirects
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMessageRedirectsDataStore
    {
        Task<IReadOnlyList<MessageRedirect>> GetRedirects();
        Task AddRedirect(MessageRedirect redirect);
        Task UpdateRedirect(MessageRedirect redirect);
        Task RemoveRedirect(MessageRedirect redirect);
    }
}
