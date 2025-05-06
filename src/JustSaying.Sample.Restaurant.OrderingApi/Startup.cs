using JustSaying.AwsTools;
using JustSaying.Sample.Restaurant.Models;
using JustSaying.Sample.Restaurant.OrderingApi.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Serilog;

namespace JustSaying.Sample.Restaurant.OrderingApi
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddConfiguration(configuration);
            configurationBuilder.AddJsonFile("local.settings.json", true, true);
            _configuration = configurationBuilder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers()
                    .SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

            services.AddJustSaying(config =>
            {
                config.Client(x =>
                {
                    x.WithBasicCredentials(_configuration["Aws:AccessKey"], _configuration["Aws:SecretAccessKey"]);
                });
                config.Messaging(x =>
                {
                    // Configures which AWS Region to operate in
                    x.WithRegion(_configuration["Aws:Region"]);
                });
                config.Subscriptions(x =>
                {
                    // Creates the following if they do not already exist
                    //  - a SQS queue of name `orderreadyevent`
                    //  - a SQS queue of name `orderreadyevent_error`
                    //  - a SNS topic of name `orderreadyevent`
                    //  - a SNS topic subscription on topic 'orderreadyevent' and queue 'orderreadyevent'
                    x.ForTopic<OrderReadyEvent>();
                    x.ForTopic<OrderDeliveredEvent>();
                });
                config.Publications(x =>
                {
                    // Creates the following if they do not already exist
                    //  - a SNS topic of name `orderplacedevent`
                    x.WithTopic<OrderPlacedEvent>();
                    x.WithTopic<OrderOnItsWayEvent>();
                });
            });

            // Added a message handler for message type for 'OrderReadyEvent' on topic 'orderreadyevent' and queue 'orderreadyevent'
            services.AddJustSayingHandler<OrderReadyEvent, OrderReadyEventHandler>();
            services.AddJustSayingHandler<OrderDeliveredEvent, OrderDeliveredEventHandler>();

            // Add a background service that is listening for messages related to the above subscriptions
            services.AddHostedService<BusService>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Restaurant Ordering API", Version = "v1" });
            });
        }

        public static void Configure(IApplicationBuilder app)
        {
            app.UseDeveloperExceptionPage();

            app.UseSerilogRequestLogging();

            app.UseRouting();
            app.UseEndpoints((endpoints) => endpoints.MapDefaultControllerRoute());

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Restaurant Ordering API");
                c.RoutePrefix = string.Empty;
            });
        }
    }
}
