namespace ServiceControl.Transports.PostgreSql
{
    class NameHelper
    {
        const string Delimiter = "\"";
        static readonly string EscapedDelimiter = Delimiter + Delimiter;

        public static string Quote(string name)
        {
            if (name.StartsWith(EscapedDelimiter) && name.EndsWith(EscapedDelimiter))
            {
                return name;
            }

            return Delimiter + name.Replace(Delimiter, EscapedDelimiter) + Delimiter;
        }
    }
}