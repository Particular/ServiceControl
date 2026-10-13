namespace ServiceControl.Api
{
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts;

    public interface IPlatformHealthApi
    {
        Task<PlatformHealthView> GetHealth(string baseUrl, CancellationToken cancellationToken = default);
    }
}