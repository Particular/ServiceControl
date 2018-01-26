namespace ServiceControl.Infrastructure.Settings
{
    using System;
    using System.Text;

    static class InstanceIdGenerator
    {
        /// <summary>
        /// Converts a string to a base64 encoded string using UTF8
        /// </summary>
        public static string FromApiUrl(string apiUrl) => Convert.ToBase64String(Encoding.UTF8.GetBytes(apiUrl.ToLowerInvariant()));

        /// <summary>
        /// Converts from a base64 encoded string value using UTF8
        /// </summary>
        public static string ToApiUrl(string instanceId) => Encoding.UTF8.GetString(Convert.FromBase64String(instanceId));
    }
}
