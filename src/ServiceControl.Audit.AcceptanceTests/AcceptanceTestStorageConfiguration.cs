namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class AcceptanceTestStorageConfiguration
    {
        // Used by the RavenDB storage configuration to serialize database lifecycle operations
        // (create/delete) that cannot run concurrently on the embedded RavenDB server.
        // No-op for InMemory; see the RavenDB project's AcceptanceTestStorageConfiguration for usage.
        public static readonly SemaphoreSlim DatabaseLifecycleLock = new(1, 1);

        public string PersistenceType { get; } = "InMemory";

        public Task<IDictionary<string, string>> CustomizeSettings() => Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>());

        public Task Cleanup() => Task.CompletedTask;
    }
}