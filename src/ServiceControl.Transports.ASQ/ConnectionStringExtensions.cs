namespace ServiceControl.Transports.ASQ
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    static class ConnectionStringExtensions
    {
        // using DbConnectionStringBuilder would escape account key and lead to troubles
        public static string RemoveCustomConnectionStringParts(this string connectionString, out string subscriptionTable)
        {
            subscriptionTable = null;

            var parts = new List<string>();
            var groups = ConnectionStringRegex.Matches(connectionString);
            foreach (Match match in groups)
            {
                switch (match.Groups[2].Value)
                {
                    case SubscriptionsTableName:
                        subscriptionTable = match.Groups[3].Value;
                        break;
                    default:
                        parts.Add(match.Value);
                        break;
                }
            }
            return string.Join(";", parts);
        }



        const string SubscriptionsTableName = "Subscriptions Table";

        static readonly Regex ConnectionStringRegex =
            new Regex(@"(?<key>[^=;]+)=(?<val>[^;]+(,\d+)?)", RegexOptions.Compiled);
    }
}