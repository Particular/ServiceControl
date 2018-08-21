namespace ServiceControl.Config.Extensions
{
    using System;

    static class GuidExtensions
    {
        public static string BareString(this Guid guid)
        {
            return guid.ToString("N");
        }
    }
}