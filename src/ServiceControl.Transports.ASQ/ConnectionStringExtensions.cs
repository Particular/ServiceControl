namespace ServiceControl.Transports.ASQ
{
    using System.Data.Common;

    static class ConnectionStringExtensions
    {
        public static string RemoveCustomConnectionStringParts(this string connectionString, out string subscriptionTable) =>
            connectionString
                .RemoveCustomConnectionStringPart(SubscriptionsTableName, out subscriptionTable);

        static string RemoveCustomConnectionStringPart(this string connectionString, string partName, out string schema)
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (builder.TryGetValue(partName, out var customSchema))
            {
                builder.Remove(partName);
            }

            schema = (string)customSchema;

            return builder.ConnectionString;
        }

        const string SubscriptionsTableName = "Subscriptions Table";
    }
}