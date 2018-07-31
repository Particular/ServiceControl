namespace ServiceControl.LoadTests.AuditGenerator
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Support;
    using Messages;
    using Transports;

    class Program
    {
        const string ConfigRoot = "AuditGenerator";

        static void Main(string[] args)
        {
            Start(args).GetAwaiter().GetResult();
        }

        static async Task Start(string[] args)
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
            transportSettings.Set("TransportSettings.RemoteInstances", Array.Empty<string>());
            transportSettings.Set("TransportSettings.RemoteTypesToSubscribeTo", Array.Empty<Type>());
            
            customization.CustomizeEndpoint(config, transportSettings);

            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo("error");
            config.EnableInstallers();
            config.RegisterComponents(c => c.RegisterSingleton(new LoadGenerators(GenerateMessages, minLength, maxLength)));

            endpointInstance = await Endpoint.Start(config);

            Console.ReadLine();
        }


        static async Task GenerateMessages(string destination, QueueInfo queueInfo, CancellationToken token)
        {
            var throttle = new SemaphoreSlim(SettingsReader<int>.Read(ConfigRoot, "ConcurrentSends", 32));
            var bodySize = SettingsReader<int>.Read(ConfigRoot, "BodySize", 0);

            var random = new Random();

            var sendMeter = Metric.Meter(destination, Unit.Custom("audits"));

            while (!token.IsCancellationRequested)
            {
                await throttle.WaitAsync(token).ConfigureAwait(false);
                // We don't need to wait for this task
                // ReSharper disable once UnusedVariable
                var sendTask = Task.Run(async () =>
                {
                    try
                    {
                        var ops = new SendOptions();

                        ops.SetHeader(Headers.HostId, HostId);
                        ops.SetHeader(Headers.HostDisplayName, "Load Generator");

                        ops.SetHeader(Headers.ProcessingMachine, RuntimeEnvironment.MachineName);
                        ops.SetHeader(Headers.ProcessingEndpoint, "LoadGenerator");

                        var now = DateTime.UtcNow;
                        ops.SetHeader(Headers.ProcessingStarted, DateTimeExtensions.ToWireFormattedString(now));
                        ops.SetHeader(Headers.ProcessingEnded, DateTimeExtensions.ToWireFormattedString(now));

                        ops.SetDestination(destination);

                        var auditMessage = new AuditMessage();
                        auditMessage.Data = new byte[bodySize];
                        random.NextBytes(auditMessage.Data);

                        await endpointInstance.Send(auditMessage, ops).ConfigureAwait(false);
                        queueInfo.Sent();
                        sendMeter.Mark();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    finally
                    {
                        throttle.Release();
                    }
                }, token);
            }
        }

        static IEndpointInstance endpointInstance;
        public static string HostId;
    }
}