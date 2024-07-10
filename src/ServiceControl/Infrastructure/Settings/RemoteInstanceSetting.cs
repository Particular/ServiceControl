namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Text.Json.Serialization;
    using ServiceControl.Infrastructure.Settings;

    public class RemoteInstanceSetting
    {
        public RemoteInstanceSetting(string baseAddress)
        {
            BaseAddress = (baseAddress ?? throw new ArgumentNullException(nameof(baseAddress))).Replace("/api", string.Empty);
            InstanceId = InstanceIdGenerator.FromApiUrl(BaseAddress);
        }

        [JsonPropertyName("api_uri")] // for legacy reasons this property is serialized as api_uri
        public string BaseAddress { get; }

        /// <summary>
        /// If we fail to connect to a remote instance, it will be temporarily disabled. Any <see cref="ScatterGatherApiBase" /> query will skip disabled instances. The <see cref="CheckRemotes" /> custom check will enable any disabled remote instance the first time it succeeds in connecting it. 
        /// </summary>
        [JsonIgnore]
        public bool TemporarilyUnavailable { get; set; }

        [JsonIgnore]
        public string InstanceId { get; }
    }
}