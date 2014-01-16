namespace ServiceControl.Plugin
{
    using System;
    using System.Collections.Generic;

    static class HostInformationRetriever
    {
        public static bool TryToRetrieveHostInfo(out string hostId, out Dictionary<string, string> parameters)
        {
            hostId = null;
            parameters = null;

            var hostInformationType = Type.GetType("NServiceBus.Hosting.HostInformation, NServiceBus.Core", false);
            if (hostInformationType == null)
            {
                return false;
            }

            parameters = (Dictionary<string, string>)hostInformationType.GetProperty("Properties").GetValue(null, null);
            hostId = (string) hostInformationType.GetProperty("HostId").GetValue(null, null);
            
            return true;
        }
    }
}