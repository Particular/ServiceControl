namespace ServiceControl.Persistence
{
    using System;
    using System.Threading.Tasks;
    using Notifications;

    public interface INotificationsManager : IDataSessionManager
    {
        Task<NotificationsSettings> LoadSettings(TimeSpan? cacheTimeout = null);
    }
}