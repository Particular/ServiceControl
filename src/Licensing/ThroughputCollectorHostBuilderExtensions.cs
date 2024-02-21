﻿namespace Particular.License
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.License.Contracts;

    public static class ThroughputCollectorHostBuilderExtensions
    {
        public static IHostApplicationBuilder AddThroughputCollector(this IHostApplicationBuilder hostBuilder, string broker, string serviceControlAPI, string errorQueue, string auditQueue, string transportConnectionString, string persistenceType)
        {
            var services = hostBuilder.Services;
            services.AddHostedService(provider => new ThroughputCollectorHostedService(provider.GetRequiredService<ILoggerFactory>(),
                                                                                        new PlatformData { Broker = broker, ServiceControlAPI = serviceControlAPI, AuditQueue = auditQueue, ErrorQueue = errorQueue, TransportConnectionString = transportConnectionString, PersistenceType = persistenceType }));
            return hostBuilder;
        }
    }
}