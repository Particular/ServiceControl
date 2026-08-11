namespace ServiceControl.Connection
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class PlatformConnectionBuilder(IEnumerable<IProvidePlatformConnectionDetails> platformConnectionProviders)
        : IPlatformConnectionBuilder
    {
        public async Task<PlatformConnectionDetails> BuildPlatformConnection(CancellationToken cancellationToken = default)
        {
            var connectionDetails = new PlatformConnectionDetails();

            await Task.WhenAll(
                from provider in platformConnectionProviders
                select provider.ProvideConnectionDetails(connectionDetails, cancellationToken)
            );

            return connectionDetails;
        }
    }
}