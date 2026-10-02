namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Linq;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Infrastructure;

    // the /api/environment endpoint is polled by the primary instance for its usage report. This currently needs to be anonymous
    [AllowAnonymous]
    [ApiController]
    [Route("api")]
    public class EnvironmentController(IEnumerable<IEnvironmentDataProvider> providers, IEnumerable<IStorageIdentityProvider> storageIdentityProviders, ILogger<EnvironmentController> logger) : ControllerBase
    {
        [Route("environment")]
        [HttpGet]
        public async Task<OkObjectResult> Environment(CancellationToken cancellationToken = default)
        {
            var environmentData = new Dictionary<string, string>();

            foreach (var provider in providers)
            {
                EnvironmentDatum[] data;

                try
                {
                    data = [.. provider.GetData()];
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Environment data provider {EnvironmentDataProvider} could not list what it offers, so none of its data is served", provider.GetType().Name);
                    continue;
                }

                foreach (var datum in data)
                {
                    try
                    {
                        environmentData[datum.Key] = await datum.ReadValue(cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "Environment datum {EnvironmentDatum} could not be read", datum.Key);
                        environmentData[datum.Key] = EnvironmentDatum.ReadFailed;
                    }
                }
            }

            return Ok(new EnvironmentDataResponse
            {
                EnvironmentData = environmentData,
                MachineIdHash = MachineIdentity.Hash,
                StorageIdentity = await ReadStorageIdentity(cancellationToken)
            });
        }

        async Task<StorageIdentityResponse> ReadStorageIdentity(CancellationToken cancellationToken)
        {
            var provider = storageIdentityProviders.FirstOrDefault();

            if (provider is null)
            {
                return null;
            }

            try
            {
                var identity = await provider.GetIdentity(cancellationToken);

                if (identity is null)
                {
                    return null;
                }

                return new StorageIdentityResponse
                {
                    Engine = identity.Engine,
                    ServerHash = IdentityHash.Compute(identity.Server),
                    DatabaseHash = IdentityHash.Compute(identity.Database),
                    SchemaHash = identity.Schema is null ? null : IdentityHash.Compute(identity.Schema)
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "The storage identity could not be read, so it is not served");

                return null;
            }
        }

        public class EnvironmentDataResponse
        {
            public Dictionary<string, string> EnvironmentData { get; set; }
            public string MachineIdHash { get; set; }
            public StorageIdentityResponse StorageIdentity { get; set; }
        }

        public class StorageIdentityResponse
        {
            public string Engine { get; set; }
            public string ServerHash { get; set; }
            public string DatabaseHash { get; set; }
            public string SchemaHash { get; set; }
        }
    }
}
