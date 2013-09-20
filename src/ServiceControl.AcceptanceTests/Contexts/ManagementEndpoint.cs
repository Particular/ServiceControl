namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.IO;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;

    public class ManagementEndpoint : EndpointConfigurationBuilder
    {
        public ManagementEndpoint()
        {
            var pathToAppConfig = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            File.WriteAllText(pathToAppConfig, String.Format(AppConfigFile, AcceptanceTest.RavenPath));

            EndpointSetup<ManagementEndpointSetup>(c=>Configure.Features.Disable<Audit>())
                .AppConfig(pathToAppConfig);
        }

        const string AppConfigFile = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<configuration>
  <configSections>
    <section name=""TransportConfig"" type=""NServiceBus.Config.TransportConfig, NServiceBus.Core"" />
    <section name=""MessageForwardingInCaseOfFaultConfig"" type=""NServiceBus.Config.MessageForwardingInCaseOfFaultConfig, NServiceBus.Core"" />
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
  <runtime>
    <assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"">
      <dependentAssembly>
        <assemblyIdentity name=""Microsoft.AspNet.SignalR.Core"" publicKeyToken=""31bf3856ad364e35"" culture=""neutral"" />
        <bindingRedirect oldVersion=""0.0.0.0-1.1.0.0"" newVersion=""1.1.0.0"" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>
";
    }
}