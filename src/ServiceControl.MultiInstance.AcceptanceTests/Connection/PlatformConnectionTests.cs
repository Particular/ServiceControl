﻿namespace ServiceControl.MultiInstance.AcceptanceTests
{
    using System.Text.Json;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControl.AcceptanceTesting;
    using TestSupport;
    using TestSupport.EndpointTemplates;


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

            Approver.Verify(JsonSerializer.Deserialize<dynamic>(config.Connection));
        }

        class MyEndpoint : EndpointConfigurationBuilder
        {
            public MyEndpoint() => EndpointSetup<DefaultServerWithoutAudit>();
        }

        class MyContext : ScenarioContext
        {
            public string Connection { get; set; }
        }
    }
}
