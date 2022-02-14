namespace ServiceControl.LoadTests.AuditGenerator
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Messages;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Support;
    using Transports;

    class Program
    {
        const string ConfigRoot = "AuditGenerator";

        static void Main()
        {
            Start().GetAwaiter().GetResult();
        }

        static async Task Start()
        {
            Metric.Config.WithReporting(r =>
            {
                r.WithCSVReports(".", TimeSpan.FromSeconds(5));
            });

            var customizationTypeName = SettingsReader<string>.Read(ConfigRoot, "TransportCustomization", null);

            var minLength = SettingsReader<int>.Read(ConfigRoot, "MinLength", 10000);
            var maxLength = SettingsReader<int>.Read(ConfigRoot, "MaxLength", 20000);
            var endpointName = SettingsReader<string>.Read(ConfigRoot, "EndpointName", "AuditGen");
            var connectionString = SettingsReader<string>.Read(ConfigRoot, "TransportConnectionString", "");

            HostId = Guid.NewGuid().ToString("N");

            var config = new EndpointConfiguration(endpointName);
            config.AssemblyScanner().ExcludeAssemblies("ServiceControl");
            config.UseSerialization<NewtonsoftSerializer>();

            var customization = (TransportCustomization)Activator.CreateInstance(Type.GetType(customizationTypeName, true));
            var transportSettings = new TransportSettings
            {
                ConnectionString = connectionString
            };
            customization.CustomizeSendOnlyEndpoint(config, transportSettings);

            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo("error");
            config.EnableInstallers();
            //config.RegisterComponents(c => c.RegisterSingleton(new LoadGenerators(GenerateMessages, minLength, maxLength)));

            endpointInstance = await Endpoint.Start(config);

            await GenerateMessages("audit").ConfigureAwait(false);

            Console.ReadLine();
        }


        static async Task GenerateMessages(string destination)
        {
            var throttle = new SemaphoreSlim(SettingsReader<int>.Read(ConfigRoot, "ConcurrentSends", 32));
            var bodySize = SettingsReader<int>.Read(ConfigRoot, "BodySize", 0);

            var random = new Random();

            //await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            // We don't need to wait for this task
            try
            {
                var ops = new SendOptions();

                ops.SetMessageId(Guid.NewGuid().ToString());
                ops.SetHeader(Headers.HostId, HostId);
                ops.SetHeader(Headers.HostDisplayName, "Load Generator");

                ops.SetHeader(Headers.ProcessingMachine, RuntimeEnvironment.MachineName);
                ops.SetHeader(Headers.ProcessingEndpoint, "LoadGenerator");

                var processingStart = DateTime.UtcNow;
                var processingEnd = processingStart.AddMilliseconds(random.Next(10, 1000));

                ops.SetHeader(Headers.TimeSent, DateTimeExtensions.ToWireFormattedString(processingStart.Subtract(TimeSpan.FromMilliseconds(random.Next(10, 2000)))));
                ops.SetHeader(Headers.ProcessingStarted, DateTimeExtensions.ToWireFormattedString(processingStart));
                ops.SetHeader(Headers.ProcessingEnded, DateTimeExtensions.ToWireFormattedString(processingEnd));
                ops.SetHeader("NServiceBus.ProcessingTime", (processingEnd - processingStart).ToString());

                ops.SetDestination(destination);

                var auditMessage = new AuditMessage
                {
                    Data = new byte[bodySize]
                };
                random.NextBytes(auditMessage.Data);

                await endpointInstance.Send(auditMessage, ops).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                throttle.Release();
            }
        }

        static IEndpointInstance endpointInstance;
        public static string HostId;
    }
}