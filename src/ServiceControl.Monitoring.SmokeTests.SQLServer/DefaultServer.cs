namespace ServiceControl.Monitoring.SmokeTests.ASQ
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Hosting.Helpers;
    using NServiceBus.ObjectBuilder;
    using NServiceBus;
    using NServiceBus.Transport;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using System.Data.SqlClient;

    public class DefaultServer : IEndpointSetupTemplate
    {
        public static string ConnectionString => GetEnvironmentVariable("SqlServerTransport.ConnectionString");
        // default if needed, ?? @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=SC_smoke_testing;Integrated Security=True"; 

        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var builder = new EndpointConfiguration(endpointConfiguration.EndpointName);
            var types = GetTypesScopedByTestClass(endpointConfiguration);

            builder.TypesToIncludeInScan(types);

            builder.EnableInstallers();

            queueBindings = builder.GetSettings().Get<QueueBindings>();

            connectionString = ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("The 'SqlServerTransport.ConnectionString' environment variable is not set.");
            }

            var transportConfig = builder.UseTransport<SqlServerTransport>();
            transportConfig.ConnectionString(connectionString);

            var routingConfig = transportConfig.Routing();

            foreach (var publisher in endpointConfiguration.PublisherMetadata.Publishers)
            {
                foreach (var eventType in publisher.Events)
                {
                    routingConfig.RegisterPublisher(eventType, publisher.PublisherName);
                }
            }

            builder.UsePersistence<InMemoryPersistence>();

            builder.Recoverability().Delayed(delayedRetries => delayedRetries.NumberOfRetries(0));
            builder.Recoverability().Immediate(immediateRetries => immediateRetries.NumberOfRetries(0));

            builder.RegisterComponents(r => { RegisterInheritanceHierarchyOfContextOnContainer(runDescriptor, r); });

            configurationBuilderCustomization(builder);

            return Task.FromResult(builder);
        }

        public static string GetEnvironmentVariable(string variable)
        {
            var candidate = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User);

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return Environment.GetEnvironmentVariable(variable);
            }

            return candidate;
        }

        static void RegisterInheritanceHierarchyOfContextOnContainer(RunDescriptor runDescriptor, IConfigureComponents r)
        {
            var type = runDescriptor.ScenarioContext.GetType();
            while (type != typeof(object))
            {
                r.RegisterSingleton(type, runDescriptor.ScenarioContext);
                type = type.BaseType;
            }
        }

        static IEnumerable<Type> GetTypesScopedByTestClass(EndpointCustomizationConfiguration endpointConfiguration)
        {
            var assemblies = new AssemblyScanner().GetScannableAssemblies();

            var types = assemblies.Assemblies
                //exclude all test types by default
                .Where(a =>
                {
                    var references = a.GetReferencedAssemblies();

                    return references.All(an => an.Name != "nunit.framework");
                })
                .SelectMany(a => a.GetTypes());


            types = types.Union(GetNestedTypeRecursive(endpointConfiguration.BuilderType.DeclaringType, endpointConfiguration.BuilderType));

            types = types.Union(endpointConfiguration.TypesToInclude);

            return types.Where(t => !endpointConfiguration.TypesToExclude.Contains(t)).ToList();
        }

        static IEnumerable<Type> GetNestedTypeRecursive(Type rootType, Type builderType)
        {
            if (rootType == null)
            {
                throw new InvalidOperationException("Make sure you nest the endpoint infrastructure inside the TestFixture as nested classes");
            }

            yield return rootType;

            if (typeof(IEndpointConfigurationFactory).IsAssignableFrom(rootType) && rootType != builderType)
            {
                yield break;
            }

            foreach (var nestedType in rootType.GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SelectMany(t => GetNestedTypeRecursive(t, builderType)))
            {
                yield return nestedType;
            }
        }

        public Task Cleanup()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var queueAddresses = queueBindings.ReceivingAddresses.Select(QueueAddress.Parse).ToList();
                foreach (var address in queueAddresses)
                {
                    TryDeleteTable(conn, address);
                    TryDeleteTable(conn, new QueueAddress(address.TableName.Trim('[', ']') + ".Delayed", address.SchemaName));
                }
            }
            return Task.FromResult(0);
        }

        static void TryDeleteTable(SqlConnection conn, QueueAddress address)
        {
            try
            {
                using (var comm = conn.CreateCommand())
                {
                    comm.CommandText = $"IF OBJECT_ID('{address}', 'U') IS NOT NULL DROP TABLE {address}";
                    comm.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("it does not exist or you do not have permission"))
                {
                    throw;
                }
            }
        }

        string connectionString;
        QueueBindings queueBindings;

        class QueueAddress
        {
            public QueueAddress(string tableName, string schemaName)
            {
                TableName = SafeQuote(tableName);
                SchemaName = SafeQuote(schemaName);
            }

            public string TableName { get; }
            public string SchemaName { get; }

            public static QueueAddress Parse(string address)
            {
                var firstAtIndex = address.IndexOf("@", StringComparison.Ordinal);

                if (firstAtIndex == -1)
                {
                    return new QueueAddress(address, null);
                }

                var tableName = address.Substring(0, firstAtIndex);
                address = firstAtIndex + 1 < address.Length ? address.Substring(firstAtIndex + 1) : string.Empty;

                var schemaName = ExtractSchema(address);
                return new QueueAddress(tableName, schemaName);
            }

            static string ExtractSchema(string address)
            {
                var noRightBrackets = 0;
                var index = 1;

                while (true)
                {
                    if (index >= address.Length)
                    {
                        return address;
                    }
                    if (address[index] == '@' && (address[0] != '[' || noRightBrackets % 2 == 1))
                    {
                        return address.Substring(0, index);
                    }

                    if (address[index] == ']')
                    {
                        noRightBrackets++;
                    }
                    index++;
                }
            }

            static string SafeQuote(string identifier)
            {
                if (string.IsNullOrWhiteSpace(identifier))
                {
                    return identifier;
                }

                using (var sanitizer = new SqlCommandBuilder())
                {
                    return sanitizer.QuoteIdentifier(sanitizer.UnquoteIdentifier(identifier));
                }
            }

            public override string ToString()
            {
                if (SchemaName == null)
                {
                    return TableName;
                }
                return $"{SchemaName}.{TableName}";
            }
        }
    }
}