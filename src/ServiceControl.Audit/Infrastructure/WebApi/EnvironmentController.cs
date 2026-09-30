namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Audit.Persistence;

    // the /api/environment endpoint is polled by the primary instance for its usage report. This currently needs to be anonymous
    [AllowAnonymous]
    [ApiController]
    [Route("api")]
    public class EnvironmentController(IEnumerable<IEnvironmentDataProvider> providers, ILogger<EnvironmentController> logger) : ControllerBase
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

            return Ok(new EnvironmentDataResponse { EnvironmentData = environmentData });
        }

        public class EnvironmentDataResponse
        {
            public Dictionary<string, string> EnvironmentData { get; set; }
        }
    }
}
