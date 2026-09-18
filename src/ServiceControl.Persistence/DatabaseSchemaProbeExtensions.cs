namespace ServiceControl.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    public static class DatabaseSchemaProbeExtensions
    {
        public static async Task EnsureDatabaseSchemaIsCurrent(this IServiceProvider services, CancellationToken cancellationToken = default)
        {
            await using var scope = services.CreateAsyncScope();

            if (scope.ServiceProvider.GetService<IDatabaseSchemaProbe>() is { } probe)
            {
                await probe.EnsureCurrent(cancellationToken);
            }
        }
    }
}
