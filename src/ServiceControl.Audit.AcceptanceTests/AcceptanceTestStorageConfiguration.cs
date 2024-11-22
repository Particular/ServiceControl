namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; } = "InMemory";

        public Task<IDictionary<string, string>> CustomizeSettings() => Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>());

        public Task Cleanup() => Task.CompletedTask;
    }
}