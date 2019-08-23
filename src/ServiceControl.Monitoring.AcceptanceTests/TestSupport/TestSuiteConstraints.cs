namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Linq;
    using ServiceBus.Management.AcceptanceTests;

    public class TestSuiteConstraints
    {
        public static ITransportIntegration CreateTransportConfiguration() => 
            GetTransportIntegrationFromConnectionFile()
            ?? GetTransportIntegrationFromEnvironmentVar()
            ?? new ConfigureEndpointMsmqTransport();

        static ITransportIntegration GetTransportIntegrationFromEnvironmentVar() => CreateTransportIntegration(
                Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.TransportCustomization"),
                Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.ConnectionString")
            );

        static ITransportIntegration GetTransportIntegrationFromConnectionFile()
        {
            var connectionFile = GetConnectionFile();
            if (connectionFile == null || !File.Exists(connectionFile))
            {
                return null;
            }
            
            var lines = File.ReadLines(connectionFile)
                .Skip(1) // Name
                .Take(2) // TransportCustomizationTypeName, ConnectionString
                .ToArray();

            return CreateTransportIntegration(lines[0], lines[1]);

        }

        static string GetConnectionFile()
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;
            while (directory != null)
            {
                if (Directory.EnumerateFiles(directory).Any(file => file.EndsWith(".sln")))
                {
                    return Path.Combine(directory, "connection.txt");
                }

                var parent = Directory.GetParent(directory);

                directory = parent?.FullName;
            }

            return null;
        }

        static ITransportIntegration CreateTransportIntegration(string transportCustomizationToUseString, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(transportCustomizationToUseString))
            {
                return null;
            }

            var transportToUse = (ITransportIntegration)Activator.CreateInstance(Type.GetType(transportCustomizationToUseString));
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                transportToUse.ConnectionString = connectionString;
            }

            return transportToUse;
        }
    }
}