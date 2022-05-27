namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using Newtonsoft.Json;

    public class RemoteInstanceSetting
    {
        public string ApiUri { get; set; }
        /// <summary>
        /// If for any reason we fail to connect to remove instance they will be disabled. Any <see cref="ScatterGatherApiBase"/> query will skip that instance. <see cref="CheckRemotes"/> custom check takes care of that. 
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