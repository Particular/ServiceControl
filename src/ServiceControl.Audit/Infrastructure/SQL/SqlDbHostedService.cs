namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using SQL;

    class SqlDbHostedService : IHostedService
    {
        readonly SqlQueryStore queryStore;
        readonly SqlStore writeStore;
        readonly SqlBodyStore bodyStore;

        public SqlDbHostedService(SqlQueryStore queryStore, SqlStore writeStore, SqlBodyStore bodyStore)
        {
            this.queryStore = queryStore;
            this.writeStore = writeStore;
            this.bodyStore = bodyStore;
        }

        public Task StartAsync(CancellationToken cancellationToken) => SetupDatabase();

        public async Task SetupDatabase()
        {
            Logger.Info("SqlDatabase initialization starting");
            await queryStore.Initialize().ConfigureAwait(false);
            await writeStore.Initialize().ConfigureAwait(false);
            await bodyStore.Initialize().ConfigureAwait(false);
            Logger.Info("SqlDatabase initialization complete");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(SqlDbHostedService));
    }
}