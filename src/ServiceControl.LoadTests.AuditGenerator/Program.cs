namespace ServiceControl.LoadTests.AuditGenerator
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Support;
    using Messages;

    class Program
    {
        static IEndpointInstance endpointInstance;
        static Guid hostId;

        static void Main(string[] args)
        {
            Start(args).GetAwaiter().GetResult();
        }

        static async Task Start(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: AuditGenerator <endpoint-name> <min-length> <max-length>");
                return;
            }

            Metric.Config.WithReporting(r =>
            {
                r.WithCSVReports(".", TimeSpan.FromSeconds(5));
                //r.WithConsoleReport(TimeSpan.FromSeconds(5));
            });

            var endpointName = args[0];
            var minLength = int.Parse(args[1]);
            var maxLength = int.Parse(args[2]);

            hostId = Guid.NewGuid();

            var config = new EndpointConfiguration(endpointName);
            config.UseTransport<MsmqTransport>();
            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo("error");
            config.EnableInstallers();
            config.RegisterComponents(c => c.RegisterSingleton(new LoadGenerators(GenerateMessages, minLength, maxLength)));

            endpointInstance = await Endpoint.Start(config);

            Console.ReadLine();
        }


        static async Task GenerateMessages(string destination, CancellationToken token)
        {
            var throttle = new SemaphoreSlim(32);

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

                        ops.SetHeader(Headers.HostId, hostId.ToString("N"));
                        ops.SetHeader(Headers.HostDisplayName, "Load Generator");

                        ops.SetHeader(Headers.ProcessingMachine, RuntimeEnvironment.MachineName);
                        ops.SetHeader(Headers.ProcessingEndpoint, "LoadGenerator");

                        var now = DateTime.UtcNow;
                        ops.SetHeader(Headers.ProcessingStarted, DateTimeExtensions.ToWireFormattedString(now));
                        ops.SetHeader(Headers.ProcessingEnded, DateTimeExtensions.ToWireFormattedString(now));

                        ops.SetDestination(destination);

                        await endpointInstance.Send(new AuditMessage(), ops).ConfigureAwait(false);
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
    }
}
