namespace ServiceControl.Audit.Auditing.BodyStorage
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence;

    public interface IBodyStorage
    {
        Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken = default);
        Task<MessageBodyView> TryFetch(string bodyId, CancellationToken cancellationToken = default);
    }
}