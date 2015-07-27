namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using NServiceBus;

    public class SqlServerTransportIntegration : ITransportIntegration
    {
        public SqlServerTransportIntegration()
        {
            ConnectionString = @"Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;"; // Default connstr
        }

        public string Name { get { return "SqlServer"; } }
        public Type Type { get { return typeof(SqlServer); } }
        public string TypeName { get { return "NServiceBus.SqlServer, NServiceBus.Transports.SqlServer"; } }
        public string ConnectionString { get; set; }

        public void Cleanup(ITransportIntegration transport)
        {
            var name = Configure.EndpointName;
            var queuesToBeDeleted = new List<string>();

            var connection = new SqlConnection(transport.ConnectionString);
            connection.Open();

            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                List<string> allQueues;
                using (var getAllQueueCommand = new SqlCommand("SELECT [name] FROM sysobjects WHERE [type] = 'U' AND category = 0 ORDER BY [name]", connection, transaction))
                {
                    using (var reader = getAllQueueCommand.ExecuteReader())
                    {
                        allQueues = new List<string>();
                        while (reader.Read())
                        {
                            allQueues.Add((string)reader[0]);
                        }
                    }
                }

                foreach (var messageQueue in allQueues)
                {
                    if (messageQueue.Equals("error", StringComparison.OrdinalIgnoreCase))
                    {
                        queuesToBeDeleted.Add(messageQueue);
                        continue;
                    }

                    if (messageQueue.Equals("audit", StringComparison.OrdinalIgnoreCase))
                    {
                        queuesToBeDeleted.Add(messageQueue);
                        continue;
                    }

                    if (messageQueue.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    {
                        queuesToBeDeleted.Add(messageQueue);
                    }
                }

                foreach (var queueName in queuesToBeDeleted)
                {
                    using (var truncateCommand = new SqlCommand("TRUNCATE TABLE [" + queueName + "]", connection, transaction))
                    {
                        truncateCommand.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }

    }
}
