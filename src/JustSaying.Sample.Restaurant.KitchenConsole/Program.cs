using System;
using System.Threading.Tasks;
using JustSaying.Sample.Restaurant.KitchenConsole.Handlers;
using JustSaying.Sample.Restaurant.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

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
                    config.AddEnvironmentVariables();
                })
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;
                    services.AddJustSaying(config =>
                    {
                        config.Client(x =>
                        {
                            if (configuration.HasAWSServiceUrl())
                            {
                                // The AWS client SDK allows specifying a custom HTTP endpoint.
                                // For testing purposes it is useful to specify a value that
                                // points to a docker image such as `p4tin/goaws` or `localstack/localstack`
                                x.WithServiceUri(configuration.GetAWSServiceUri())
                                 .WithAnonymousCredentials();
                            }
                            else
                            {
                                // The real AWS environment will require some means of authentication
                                //x.WithBasicCredentials("###", "###");
                                //x.WithSessionCredentials("###", "###", "###");
                            }
                        });

                        config.Messaging(x =>
                        {
                            // Configures which AWS Region to operate in
                            x.WithRegion(configuration.GetAWSRegion());
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
                                    .WithReadConfiguration(rc  =>
                                        rc.WithSubscriptionGroup("GroupA")));

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
                    });

                    // Added a message handler for message type for 'OrderPlacedEvent' on topic 'orderplacedevent' and queue 'orderplacedevent'
                    services.AddJustSayingHandler<OrderPlacedEvent, OrderPlacedEventHandler>();
                    services.AddJustSayingHandler<OrderOnItsWayEvent, OrderOnItsWayEventHandler>();

                    // Add a background service that is listening for messages related to the above subscriptions
                    services.AddHostedService<Subscriber>();
                })
                .UseConsoleLifetime()
                .Build()
                .RunAsync();
        }
    }
}
