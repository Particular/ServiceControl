namespace ServiceControl.Transports.ASQ
{
    using System.Data.Common;

    static class ConnectionStringExtensions
    {
        public static string RemoveCustomConnectionStringParts(this string connectionString, out string subscriptionTable) =>
            connectionString
                .RemoveCustomConnectionStringPart(SubscriptionsTableName, out subscriptionTable);

        static string RemoveCustomConnectionStringPart(this string connectionString, string partName, out string value)
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (builder.TryGetValue(partName, out var partValue))
            {
                builder.Remove(partName);
            }

            value = (string)partValue;

            return builder.ConnectionString;
        }

        const string SubscriptionsTableName = "Subscriptions Table";
    }
}