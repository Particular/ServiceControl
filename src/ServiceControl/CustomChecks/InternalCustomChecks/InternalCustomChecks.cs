namespace ServiceControl.CustomChecks
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Operations;
    using SagaAudit;

    static class InternalCustomChecks
    {
        public static IHostApplicationBuilder AddInternalCustomChecks(this IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddCustomCheck<CriticalErrorCustomCheck>();
            services.AddCustomCheck<CheckRemotes>();
            services.AddCustomCheck<SagaAuditMisconfigurationCustomCheck>();
            services.AddHostedService(provider => provider.GetRequiredService<InternalCustomChecksHostedService>());
            return hostBuilder;
        }
    }
}