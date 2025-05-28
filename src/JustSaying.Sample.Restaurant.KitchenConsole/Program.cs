using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using JustSaying.Fluent;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Messaging.Middleware;
using JustSaying.Messaging.Middleware.MessageContext;
using JustSaying.Sample.Restaurant.KitchenConsole.Handlers;
using JustSaying.Sample.Restaurant.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Particular.JustSaying.RetryMiddleware;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace JustSaying.Sample.Restaurant.KitchenConsole
{
    internal class Program
    {
        public static async Task Main()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .MinimumLevel.Debug()
                .Enrich.WithProperty("AppName", nameof(KitchenConsole))
                .CreateLogger();

            Console.Title = "KitchenConsole";

            try
            {
                await Run();
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Error occurred during startup: {Message}", e.Message);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static async Task Run()
        {
            await new HostBuilder()
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false);
                    config.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true);
                    config.AddJsonFile("local.settings.json", true, true);
                    config.AddEnvironmentVariables();
                })
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddJustSaying(config =>
                    {
                        AwsClientFactoryBuilder awsClientFactoryBuilder = null;
                        config.Client(x =>
                        {
                            awsClientFactoryBuilder = x.WithBasicCredentials(hostContext.Configuration["Aws:AccessKey"], hostContext.Configuration["Aws:SecretAccessKey"]);
                        });

                        config.Messaging(x =>
                        {
                            // Configures which AWS Region to operate in
                            x.WithRegion(hostContext.Configuration["Aws:Region"]);
                        });

                        config.Subscriptions(x =>
                        {
                            x.WithSubscriptionGroup("GroupA",
                                c => c.WithPrefetch(10)
                                    .WithMultiplexerCapacity(10)
                                    .WithConcurrencyLimit(5));

                            // Creates the following if they do not already exist
                            //  - a SQS queue of name `orderplacedevent`
                            //  - a SQS queue of name `orderplacedevent_error`
                            //  - a SNS topic of name `orderplacedevent`
                            //  - a SNS topic subscription on topic 'orderplacedevent' and queue 'orderplacedevent' with two tags
                            //      - "IsOrderEvent" with no value
                            //      - "Subscriber" with the value "KitchenConsole"
                            //  - a SNS topic subscription on topic 'orderonitswayevent' and queue 'orderonitswayevent'
                            x.ForTopic<OrderPlacedEvent>(cfg =>
                                cfg.WithTag("IsOrderEvent")
                                    .WithTag("Subscriber", nameof(KitchenConsole))
                                    .WithReadConfiguration(rc =>
                                        rc.WithSubscriptionGroup("GroupA"))
                                    .WithMiddlewareConfiguration(mw =>
                                    {
                                        mw.Use<RetryAcknowledgementMiddleware>();
                                        mw.UseDefaults<OrderPlacedEvent>(typeof(OrderPlacedEventHandler));
                                    }));

                            x.ForTopic<OrderOnItsWayEvent>(cfg =>
                                cfg.WithReadConfiguration(rc =>
                                    rc.WithSubscriptionGroup("GroupB")));
                        });

                        config.Publications(x =>
                        {
                            // Creates the following if they do not already exist
                            //  - a SNS topic of name `orderreadyevent` with two tags:
                            //      - "IsOrderEvent" with no value
                            //      - "Publisher" with the value "KitchenConsole"
                            x.WithTopic<OrderReadyEvent>(cfg =>
                            {
                                cfg.WithTag("IsOrderEvent")
                                    .WithTag("Publisher", nameof(KitchenConsole));
                            });
                            x.WithTopic<OrderDeliveredEvent>();
                        });
                        config.Services((options) =>
                            options.WithMessageMonitoring(() =>
                            {
                                // var sqs = services.BuildServiceProvider().GetService<IAmazonSQS>();
                                // var region = hostContext.Configuration["Aws:Region"].ToString())
                                var sqs = awsClientFactoryBuilder.Build()
                                    .GetSqsClient(RegionEndpoint.GetBySystemName(hostContext.Configuration["Aws:Region"]));
                                return new NServiceBusExceptionMonitor(sqs);
                            }));
                    });

                    // Added a message handler for message type for 'OrderPlacedEvent' on topic 'orderplacedevent' and queue 'orderplacedevent'
                    services.AddJustSayingHandler<OrderPlacedEvent, OrderPlacedEventHandler>();
                    services.AddJustSayingHandler<OrderOnItsWayEvent, OrderOnItsWayEventHandler>();

                    services.AddTransient<RetryAcknowledgementMiddleware>();
                    services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(log =>
                    {
                        var logger = log.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(KitchenConsole));
                        return (Microsoft.Extensions.Logging.ILogger)logger;
                    });
                    
                    services.AddSingleton<IAmazonSQS>(sp =>
                    {
                        var config = new AmazonSQSConfig
                        {
                            RegionEndpoint = RegionEndpoint.GetBySystemName(hostContext.Configuration["Aws:Region"])
                        };
                        return new AmazonSQSClient(config);
                    });

                    // Add a background service that is listening for messages related to the above subscriptions
                    services.AddHostedService<Subscriber>();
                })
                .UseConsoleLifetime()
                .Build()
                .RunAsync();
        }
    }
}
