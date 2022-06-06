namespace ServiceControl.Notifications.Api
{
    public class NotificationsAppSettings
    {
        public NotificationsAppSettings(string instanceName, string apiUrl, string filter, string dropFolder)
        {
            InstanceName = instanceName;
            ApiUrl = apiUrl;
            Filter = filter;
            DropFolder = dropFolder;
        }

        public string InstanceName { get; }
        public string ApiUrl { get; }
        public string Filter { get; }
        public string DropFolder { get; }
    }
}