namespace ServiceControl.Audit.AcceptanceTests.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using Audit.Monitoring;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Raven.Client;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_known_endpoint_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_update_document_expiry()
        {
            SetSettings = settings => settings.AuditRetentionPeriod = TimeSpan.FromDays(7);
            var timestamp = DateTime.UtcNow;
            KnownEndpoint knownEndpoint = null;
            var originalExpiry = timestamp.AddDays(1);
            string updatedExpiry = null;
            var hostId = Guid.NewGuid();
            var endpointName = Conventions.EndpointNamingConvention(typeof(SampleEndpoint));

            await Define<ScenarioContext>()
                .WithEndpoint<SampleEndpoint>(b => 
                    b.CustomConfig(cfg => cfg.UniquelyIdentifyRunningInstance().UsingCustomIdentifier(hostId))
                        .When(async session =>
                        {
                            knownEndpoint = new KnownEndpoint
                            {
                                LastSeen = DateTime.UtcNow,
                                Host = "Doesn't Matter",
                                Name = endpointName,
                                HostId = hostId,
                                Id = KnownEndpoint.MakeDocumentId(endpointName, hostId)
                            };
                            await this.CreateKnownEndpoint(knownEndpoint, originalExpiry);

                            var sendOptions = new SendOptions();
                            sendOptions.RouteToThisEndpoint();
                            await session.Send(new SomeMessage(), sendOptions);
                        }))
                .Done(x =>
                {
                    var knownEndpointExpiry = GetKnownEndpointExpiry(knownEndpoint).GetAwaiter().GetResult();
                    if (knownEndpointExpiry != null)
                    {
                        updatedExpiry = knownEndpointExpiry as string;
                    }
                    return updatedExpiry != originalExpiry.ToString("O");
                })
                .Run();

            var expectedExpiry = timestamp.AddDays(7);
            var updatedExpiryValue = DateTime.Parse(updatedExpiry).ToUniversalTime();
            // HINT: The expiry is based off of the message processed at time on the message and we cannot control that. We allow 2 minutes of tolerance
            Assert.IsTrue(Math.Abs((updatedExpiryValue - expectedExpiry).TotalSeconds) < 120, "Expiry should be around 7 days");
        }

        class SampleEndpoint : EndpointConfigurationBuilder
        {
            public SampleEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            class SomeMessageHandler : IHandleMessages<SomeMessage>
            {
                public Task Handle(SomeMessage message, IMessageHandlerContext context)
                {
                    return Task.CompletedTask;
                }
            }
        }

        class SomeMessage : IMessage
        {

        }

        async Task CreateKnownEndpoint(KnownEndpoint knownEndpoint, DateTime expiry)
        {
            var store = await Database.PrepareDatabase("audit");
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(knownEndpoint);
                session.Advanced.GetMetadataFor(knownEndpoint)[Constants.Documents.Metadata.Expires] =
                    expiry.ToString("O");
                await session.SaveChangesAsync();
            }
        }

        async Task<object> GetKnownEndpointExpiry(KnownEndpoint knownEndpoint)
        {
            var store = await Database.PrepareDatabase("audit");
            using (var session = store.OpenAsyncSession())
            {
                var loaded = await session.LoadAsync<KnownEndpoint>(knownEndpoint.Id);
                return session.Advanced.GetMetadataFor(loaded)[Constants.Documents.Metadata.Expires];
            }
        }
    }
}