namespace NServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a store for delayed messages.
    /// </summary>
    public interface IDelayedMessageStore
    {
        /// <summary>
        /// Initializes the storage e.g. creates required database artifacts etc.
        /// </summary>
        /// <param name="endpointName">Name of the endpoint that hosts the delayed delivery storage.</param>
        /// <param name="transactionMode">The transaction mode selected for the transport. The storage implementation should throw an exception if it can't support specified
        /// transaction mode e.g. TransactionScope mode requires the storage to enlist in a distributed transaction managed by the DTC.</param>
        /// <param name="cancellationToken">The cancellation token set if the endpoint begins to shut down while the Initialize method is executing.</param>
        Task Initialize(string endpointName, TransportTransactionMode transactionMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the date and time set for the next delayed message to become due or null if there are no delayed messages stored.
        /// </summary>
        /// <returns></returns>
        Task<DateTimeOffset?> Next(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a delayed message.
        /// </summary>
        /// <param name="entity">Object representing a delayed message.</param>
        /// <param name="cancellationToken">The cancellation token for cooperative cancellation</param>
        Task Store(DelayedMessage entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a due delayed message that has been dispatched to its destination from the store.
        /// </summary>
        /// <param name="entity">Object representing a delayed message previously returned by FetchNextDueTimeout.</param>
        /// <param name="cancellationToken">The cancellation token for cooperative cancellation</param>
        /// <returns>True if the removal succeeded. False if there was nothing to remove because the delayed message was already gone.</returns>
        Task<bool> Remove(DelayedMessage entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Increments the counter of failures for a given due delayed message.
        /// </summary>
        /// <param name="entity">Object representing a delayed message previously returned by FetchNextDueTimeout.</param>
        /// <param name="cancellationToken">The cancellation token for cooperative cancellation</param>
        /// <returns>True if the increment succeeded. False if the delayed message was already gone.</returns>
        Task<bool> IncrementFailureCount(DelayedMessage entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the oldest due delayed message from the store or returns null if there is no due delayed messages.
        /// </summary>
        /// <param name="at">The point in time to which to compare the due date of the messages.</param>
        /// <param name="cancellationToken">The cancellation token for cooperative cancellation</param>
        Task<DelayedMessage> FetchNextDueTimeout(DateTimeOffset at, CancellationToken cancellationToken = default);
    }
}