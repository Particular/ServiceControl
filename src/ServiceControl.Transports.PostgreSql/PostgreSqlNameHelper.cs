namespace ServiceControl.Transports.PostgreSql;

// NOTE: Copied from the SQL Transport

public static class PostgreSqlNameHelper
{
    const string Delimiter = "\"";
    static readonly string EscapedDelimiter = Delimiter + Delimiter;

    public static string Quote(string unquotedName)
    {
        if (unquotedName == null)
        {
            return null;
        }
        //Quotes are escaped by using double quotes
        return Delimiter + unquotedName.Replace(Delimiter, EscapedDelimiter) + Delimiter;
    }

    public static string Unquote(string quotedString)
    {
        if (quotedString == null)
        {
            return null;
        }

        if (!quotedString.StartsWith(Delimiter) || !quotedString.EndsWith(Delimiter))
        {
            //Already unquoted
            return quotedString;
        }

        return quotedString
            .Substring(Delimiter.Length, quotedString.Length - (2 * Delimiter.Length))
            .Replace(EscapedDelimiter, Delimiter);
    }
}