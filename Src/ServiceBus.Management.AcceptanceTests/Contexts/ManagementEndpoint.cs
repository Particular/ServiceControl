namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.IO;
    using NServiceBus.AcceptanceTesting;

    public class ManagementEndpoint : EndpointConfigurationBuilder
    {
        private const string AppConfigFile = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<configuration>
  <configSections>
    <section name=""TransportConfig"" type=""NServiceBus.Config.TransportConfig, NServiceBus.Core"" />
    <section name=""MessageForwardingInCaseOfFaultConfig"" type=""NServiceBus.Config.MessageForwardingInCaseOfFaultConfig, NServiceBus.Core"" />
    <section name=""log4net"" type=""log4net.Config.Log4NetConfigurationSectionHandler,log4net"" />
    <section name=""UnicastBusConfig"" type=""NServiceBus.Config.UnicastBusConfig, NServiceBus.Core"" />
  </configSections>
  <appSettings>
    <!--<add key=""ServiceBus/Management/Port"" value =""9999""/>-->
    <!--<add key=""ServiceBus/Management/VirtualDirectory"" value =""Management""/>-->
    <add key=""ServiceBus/AuditQueue"" value=""audit"" />
    <add key=""ServiceBus/ErrorQueue"" value=""error"" />
    <add key=""ServiceBus/Management/Name"" value=""Particular Management"" />
    <add key=""ServiceBus/Management/Description"" value=""Description for Particular Management"" />
    <add key=""ServiceBus/Management/TransportType"" value=""NServiceBus.Msmq, NServiceBus.Core"" />
    <add key=""ServiceBus/Management/DbPath"" value=""{0}"" />
  </appSettings>
  <connectionStrings>
    <add name=""NServiceBus/Transport"" connectionString=""cacheSendConnection=true"" />
  </connectionStrings>
  <TransportConfig MaximumConcurrencyLevel=""10"" MaxRetries=""3"" />
  <MessageForwardingInCaseOfFaultConfig ErrorQueue=""ServiceBus.Management.Errors"" />
  <UnicastBusConfig ForwardReceivedMessagesTo=""audit"">
  </UnicastBusConfig>
</configuration>
";
        public ManagementEndpoint()
        {
            string pathToAppConfig = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            File.WriteAllText(pathToAppConfig, String.Format(AppConfigFile, AcceptanceTest.RavenPath));

            EndpointSetup<ManagementEndpointSetup>()
                .AppConfig(pathToAppConfig);
        }
    }
}