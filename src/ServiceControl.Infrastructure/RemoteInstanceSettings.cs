namespace ServiceBus.Management.Infrastructure.Settings
{
    using System.Collections.Generic;

    public class RemoteInstanceSettings
    {
        readonly RemoteInstanceSetting[] remoteInstances;

        public RemoteInstanceSettings(RemoteInstanceSetting[] remoteInstances, string localApiUrl)
        {
            this.remoteInstances = remoteInstances;
            LocalApiUrl = localApiUrl;
        }

        public string LocalApiUrl { get; }
        public IReadOnlyCollection<RemoteInstanceSetting> RemoteInstances => remoteInstances;
    }
}