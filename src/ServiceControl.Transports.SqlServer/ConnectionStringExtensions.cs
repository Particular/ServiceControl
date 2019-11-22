namespace ServiceControl.Transports.SqlServer
{
    using System.Data.Common;

    static class ConnectionStringExtensions
    {
        public static string RemoveCustomConnectionStringParts(this string connectionString, out string schema, out string subscriptionTable)
        {
            return connectionString
                .RemoveCustomConnectionStringPart(queueSchemaName, out schema)
                .RemoveCustomConnectionStringPart(subscriptionsTableName, out subscriptionTable);
        }

        public static string RemoveCustomConnectionStringPart(this string connectionString, string partName, out string schema)
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

        const string queueSchemaName = "Queue Schema";
        const string subscriptionsTableName = "Subscriptions Table";
    }
}