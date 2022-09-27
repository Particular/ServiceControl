namespace ServiceControl.Audit.AcceptanceTests
{
    using System;
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
                DataStoreTypeName = nameof(DataStoreType.RavenDb5),
                DatabaseName = Guid.NewGuid().ToString(),
            };

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}
