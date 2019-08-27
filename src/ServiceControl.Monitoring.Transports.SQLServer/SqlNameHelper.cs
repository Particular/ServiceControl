namespace ServiceControl.Transports.SQLServer
{
    class SqlNameHelper
    {
        const string prefix = "[";
        const string suffix = "]";

        public static string Quote(string name)
        {
            if (name == null)
            {
                return null;
            }

            if (name.StartsWith(prefix) && name.EndsWith(suffix))
            {
                return name;
            }

            return prefix + name.Replace(suffix, suffix + suffix) + suffix;
        }

        public static string Unquote(string quotedString)
        {
            if (quotedString == null)
            {
                return null;
            }

            if (!quotedString.StartsWith(prefix) || !quotedString.EndsWith(suffix))
            {
                return quotedString;
            }

            return quotedString
                .Substring(prefix.Length, quotedString.Length - prefix.Length - suffix.Length).Replace(suffix + suffix, suffix);
        }
    }
}