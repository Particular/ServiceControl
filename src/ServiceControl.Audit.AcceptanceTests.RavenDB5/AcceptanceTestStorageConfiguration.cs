namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Threading.Tasks;
    using ServiceControl.AcceptanceTesting;
    using ServiceControl.Audit.Infrastructure.Settings;

    partial class AcceptanceTestStorageConfiguration
    {
        public DataStoreConfiguration DataStoreConfiguration { get; protected set; }

        public Task Configure()
        {
            DataStoreConfiguration = new DataStoreConfiguration
            {
                DataStoreTypeName = nameof(DataStoreType.RavenDb5)
            };

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}
