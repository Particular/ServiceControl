namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Manages the lifecycle of the MongoDB persistence layer.
    /// </summary>
    interface IMongoPersistenceLifecycle
    {
        /// <summary>
        /// Initializes the MongoDB client and verifies connectivity.
        /// </summary>
        Task Initialize(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the persistence layer and releases resources.
        /// </summary>
        Task Stop(CancellationToken cancellationToken = default);
    }
}
