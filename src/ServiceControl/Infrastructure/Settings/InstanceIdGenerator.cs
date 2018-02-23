namespace ServiceControl.Infrastructure.Settings
{
    using System;
    using System.Text;

    // NOTE: This class implements a version of https://en.wikipedia.org/wiki/Base64#URL_applications
    public static class InstanceIdGenerator
    {
        /// <summary>
        /// Converts a string to a base64 encoded string using UTF8
        /// </summary>
        public static string FromApiUrl(string apiUrl) => Convert.ToBase64String(Encoding.UTF8.GetBytes(apiUrl.ToLowerInvariant())).Replace('+','-').Replace('/','_').Replace('=','.');

        /// <summary>
        /// Converts from a base64 encoded string value using UTF8
        /// </summary>
        public static string ToApiUrl(string instanceId) => Encoding.UTF8.GetString(Convert.FromBase64String(instanceId.Replace('-', '+').Replace('_', '/').Replace('.', '=')));
    }
}
