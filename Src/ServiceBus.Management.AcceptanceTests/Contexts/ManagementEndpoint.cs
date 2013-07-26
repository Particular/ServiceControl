namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.IO;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    public class ManagementEndpoint : EndpointConfigurationBuilder
    {
        public ManagementEndpoint()
        {
            var pathToAppConfig = "ServiceBus.Management.dll.config";

            if (!File.Exists(pathToAppConfig))
            {
                pathToAppConfig = Path.Combine(Environment.CurrentDirectory,
                                               "..\\..\\..\\ServiceBus.Management\\bin\\debug\\ServiceBus.Management.dll.config");
            }

            Assert.True(File.Exists(pathToAppConfig));

            EndpointSetup<ManagementEndpointSetup>()
                .AppConfig(pathToAppConfig);

        }
    }
}