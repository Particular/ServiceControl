namespace ServiceControl.Connection
{
    using System.Linq;
    using System.Threading.Tasks;

    class PlatformConnectionBuilder : IPlatformConnectionBuilder
    {
        readonly IProvidePlatformConnectionDetails[] platformConnectionProviders;

        public PlatformConnectionBuilder(IProvidePlatformConnectionDetails[] platformConnectionProviders)
            => this.platformConnectionProviders = platformConnectionProviders;

        public async Task<PlatformConnectionDetails> BuildPlatformConnection()
        {
            var connectionDetails = new PlatformConnectionDetails();

            await Task.WhenAll(
                from provider in platformConnectionProviders
                select provider.ProvideConnectionDetails(connectionDetails)
            ).ConfigureAwait(false);

            return connectionDetails;
        }
    }
}