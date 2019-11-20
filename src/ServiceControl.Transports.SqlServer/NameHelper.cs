namespace ServiceControl.Transports.SqlServer
{
    class NameHelper
    {
        const string prefix = "[";
        const string suffix = "]";

        public static string Quote(string unquotedName)
        {
            if (unquotedName == null)
            {
                return null;
            }
            return prefix + unquotedName.Replace(suffix, suffix + suffix) + suffix;
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