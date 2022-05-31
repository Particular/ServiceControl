namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using Newtonsoft.Json;

    public class RemoteInstanceSetting
    {
        public string ApiUri { get; set; }
        /// <summary>
        /// If we fail to connect to a remote instance, it will be temporarily disabled. Any <see cref="ScatterGatherApiBase"/> query will skip disabled instances. The <see cref="CheckRemotes"/> custom check will enable any disabled remote instance the first time it succeeds in connecting it. 
        /// </summary>
        [JsonIgnore]
        public bool TemporarilyUnavailable { get; set; }

        [JsonIgnore]
        public Uri ApiAsUri
        {
            get
            {
                if (apiAsUri == null)
                {
                    apiAsUri = new Uri(ApiUri);
                }

                return apiAsUri;
            }
        }

        Uri apiAsUri;
    }
}