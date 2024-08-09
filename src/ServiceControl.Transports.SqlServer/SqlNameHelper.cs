#nullable enable
namespace ServiceControl.Transports.SqlServer
{
    static class SqlNameHelper
    {
        const string prefix = "[";
        const string suffix = "]";

        public static string Quote(string name)
        {
            if (name.StartsWith(prefix) && name.EndsWith(suffix))
            {
                return name;
            }

            return prefix + name.Replace(suffix, suffix + suffix) + suffix;
        }
    }
}