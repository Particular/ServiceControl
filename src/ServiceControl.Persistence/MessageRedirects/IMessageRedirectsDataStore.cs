namespace ServiceControl.Persistence.MessageRedirects
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMessageRedirectsDataStore
    {
        Task<IReadOnlyList<MessageRedirect>> GetRedirects(CancellationToken cancellationToken = default);
        Task AddRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default);
        Task UpdateRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default);
        Task RemoveRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default);
    }
}
