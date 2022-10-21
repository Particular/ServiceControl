namespace ServiceControl.MultiInstance.AcceptanceTests
{
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControl.AcceptanceTesting;
    using TestSupport;
    using TestSupport.EndpointTemplates;

    [RunOnAllTransports]
    class PlatformConnectionTests : AcceptanceTest
    {
        [Test]
        public async Task ExposesConnectionDetails()
        {
            var config = await Define<MyContext>()
                .WithEndpoint<MyEndpoint>()
                .Done(async x =>
                {
                    var result = await this.GetRaw("/api/connection", ServiceControlInstanceName);
                    x.Connection = await result.Content.ReadAsStringAsync();
                    return true;
                })
                .Run();

            Assert.IsNotNull(config.Connection);

            var formatted =
                JsonConvert.SerializeObject(
                    JsonConvert.DeserializeObject(config.Connection),
                    Formatting.Indented
                );

            Approver.Verify(formatted);
        }

        class MyEndpoint : EndpointConfigurationBuilder
        {
            public MyEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }
        }

        class MyContext : ScenarioContext
        {
            public string Connection { get; set; }
        }
    }
}
