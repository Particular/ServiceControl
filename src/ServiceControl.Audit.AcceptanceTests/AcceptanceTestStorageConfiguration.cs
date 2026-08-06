namespace ServiceControl.Audit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; } = "InMemory";

        public Task<IDictionary<string, string>> CustomizeSettings() => Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>());

        public Task Cleanup() => Task.CompletedTask;

        public static Task<IDisposable> UseDatabaseLifecycleLock() => Task.FromResult<IDisposable>(new EmptyDisposable());

        class EmptyDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}