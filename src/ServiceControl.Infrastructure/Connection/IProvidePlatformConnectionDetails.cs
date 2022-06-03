namespace ServiceControl.Connection
{
    using System.Threading.Tasks;

    public interface IProvidePlatformConnectionDetails
    {
        Task ProvideConnectionDetails(PlatformConnectionDetails connection);
    }
}