namespace ServiceControl.Api.Contracts
{
    using System.Collections.Generic;

    public class RemoteEnvironment
    {
        public string ApiUri { get; set; }

        /// <summary>
        /// Null when the remote could not be reached or predates the environment endpoint.
        /// </summary>
        public Dictionary<string, string> EnvironmentData { get; set; }
    }
}
