namespace ServiceControl.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;
    using Notifications;

    /// <summary>
    /// Loads and saves notification settings as detached snapshots.
    /// </summary>
    public interface INotificationsDataStore
    {
        /// <summary>
        /// Loads the persisted notification settings, or returns default settings when none have been saved.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A mutable snapshot that is not tracked by the persistence provider.</returns>
        Task<NotificationsSettings> LoadSettings(CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces the persisted notification settings with the supplied snapshot.
        /// </summary>
        /// <param name="settings">The complete settings snapshot to persist.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A task that completes after the snapshot has been persisted.</returns>
        Task SaveSettings(NotificationsSettings settings, CancellationToken cancellationToken = default);
    }
}
