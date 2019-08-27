namespace ServiceControl.Transports.SQLServer
{
    using System.Data.Common;

    static class ConnectionStringExtensions
    {
        public static string RemoveCustomSchemaPart(this string connectionString, out string schema)
        {
            const string queueSchemaName = "Queue schema";

            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (builder.TryGetValue(queueSchemaName, out var customSchema))
            {
                builder.Remove(queueSchemaName);
            }

            schema = (string)customSchema;

            return builder.ConnectionString;
        }
    }
}