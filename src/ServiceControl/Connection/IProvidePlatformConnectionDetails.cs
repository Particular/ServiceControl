namespace ServiceControl.Connection
{
    using System.Threading.Tasks;

    interface IProvidePlatformConnectionDetails
    {
        Task ProvideConnectionDetails(PlatformConnectionDetails connection);
    }
}