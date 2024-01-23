namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Text.Json.Serialization;
    using ServiceControl.Infrastructure.Settings;

    public class RemoteInstanceSetting
    {
        public RemoteInstanceSetting(string apiUri)
        {
            ApiUri = apiUri ?? throw new ArgumentNullException(nameof(apiUri));
            InstanceId = InstanceIdGenerator.FromApiUrl(ApiUri);
        }

        public string ApiUri { get; }

        /// <summary>
        /// If we fail to connect to a remote instance, it will be temporarily disabled. Any <see cref="ScatterGatherApiBase"/> query will skip disabled instances. The <see cref="CheckRemotes"/> custom check will enable any disabled remote instance the first time it succeeds in connecting it. 
        /// </summary>
        [JsonIgnore]
        public bool TemporarilyUnavailable { get; set; }

        [JsonIgnore]
        public string InstanceId { get; }
    }
}