namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using Newtonsoft.Json;

    public class RemoteInstanceSetting
    {
        public string ApiUri { get; set; }

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