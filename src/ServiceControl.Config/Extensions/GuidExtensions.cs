using System;

namespace ServiceControl.Config.Extensions
{
    internal static class GuidExtensions
    {
        public static string BareString(this Guid guid)
        {
            return guid.ToString("N");
        }
    }
}