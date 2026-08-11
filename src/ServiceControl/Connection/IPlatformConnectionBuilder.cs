namespace ServiceControl.Connection
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPlatformConnectionBuilder
    {
        Task<PlatformConnectionDetails> BuildPlatformConnection(CancellationToken cancellationToken = default);
    }
}