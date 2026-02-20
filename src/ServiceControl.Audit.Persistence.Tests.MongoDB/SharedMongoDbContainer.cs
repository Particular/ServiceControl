namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Testcontainers.MongoDb;

    /// <summary>
    /// Provides a shared MongoDB container instance for all tests in the assembly.
    /// The container is started once and reused across all tests for performance.
    /// </summary>
    static class SharedMongoDbContainer
    {
        static MongoDbContainer container;
        static readonly SemaphoreSlim semaphore = new(1, 1);
        static bool isInitialized;

        public static async Task<MongoDbContainer> GetInstance()
        {
            if (isInitialized)
            {
                return container;
            }

            await semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (isInitialized)
                {
                    return container;
                }

                container = new MongoDbBuilder()
                    .WithImage("mongo:8.0")
                    .WithName($"servicecontrol-audit-tests-{Guid.NewGuid():N}")
                    .WithPortBinding(27018, 27017)
                    .WithUsername(string.Empty) // Disable authentication for simpler testing
                    .WithPassword(string.Empty)
                    .Build();

                await container.StartAsync().ConfigureAwait(false);

                isInitialized = true;

                return container;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Gets the connection string for the MongoDB container.
        /// Includes credentials required for authentication.
        /// </summary>
        public static string GetConnectionString()
        {
            if (!isInitialized || container == null)
            {
                throw new InvalidOperationException("Container not initialized. Call GetInstance() first.");
            }

            return container.GetConnectionString();
        }

        public static async Task StopAsync()
        {
            if (container != null)
            {
                await container.DisposeAsync().ConfigureAwait(false);
                container = null;
                isInitialized = false;
            }
        }
    }
}
