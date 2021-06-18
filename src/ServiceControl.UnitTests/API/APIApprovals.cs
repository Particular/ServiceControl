namespace ServiceControl.UnitTests.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Web.Http.Controllers;
    using System.Web.Http.Hosting;
    using System.Web.Http.Routing;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Particular.Approvals;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Licensing;
    using PublicApiGenerator;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControlInstaller.Engine.Instances;
    using Transports;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void PublicClr()
        {
            var publicApi = typeof(Bootstrapper).Assembly.GeneratePublicApi(new ApiGeneratorOptions
            {
                ExcludeAttributes = new[] { "System.Reflection.AssemblyMetadataAttribute" }
            });
            Approver.Verify(publicApi);
        }

        [Test]
        public void ServiceControlTransport()
        {
            var serviceControlTransportApi = typeof(TransportSettings).Assembly.GeneratePublicApi(new ApiGeneratorOptions
            {
                ExcludeAttributes = new[] { "System.Reflection.AssemblyMetadataAttribute" }
            });
            Approver.Verify(serviceControlTransportApi);
        }

        [Test]
        public void RootPathValue()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost");
            request.Properties.Add(HttpPropertyKeys.RequestContextKey, new HttpRequestContext { VirtualPathRoot = "/" });

            var controller = new RootController(new ActiveLicense(new Settings()) { IsValid = true }, new LoggingSettings("testEndpoint"), new Settings())
            {
                Url = new UrlHelper(request)
            };

            var result = controller.Urls();

            Approver.Verify(result.Content);
        }

        [Test]
        public void TransportNames()
        {
            //HINT: Those names are used in PowerShell scripts thus constitute a public api.
            //Also Particular.PlatformSamples relies on it to specify the learning transport.
            var transportNamesType = typeof(TransportNames);
            var publicTransportNames = transportNamesType.Assembly.GeneratePublicApi(new ApiGeneratorOptions
            {
                IncludeTypes = new[] { transportNamesType },
                ExcludeAttributes = new[] { "System.Reflection.AssemblyMetadataAttribute" }
            });

            Approver.Verify(publicTransportNames);
        }

        [Test]
        public void PlatformSampleSettings()
        {
            //HINT: Particular.PlatformSample includes a parameterized version of the ServiceControl.exe.config file.
            //If any changes have been made to settings, this may break the embedded config in that project, which may need to be updated.
            var settings = new Settings
            {
                LicenseFileText = null
            };

            Approver.Verify(settings);
        }

        [Test]
        public void CustomCheckDetails()
        {
            // HINT: Custom checks are documented on the docs site and Id and Category are published in integration events
            // If any changes have been made to custom checks, this may break customer integration subscribers.
            Approver.Verify(
                string.Join(Environment.NewLine,
                    from check in GetCustomChecks()
                    orderby check.Category, check.Id
                    select $"{check.Category}: {check.Id}"
                )
            );
        }

        static IEnumerable<ICustomCheck> GetCustomChecks()
        {
            var settings = (object)new Settings();

            var serviceControlTypes = typeof(Bootstrapper).Assembly
                .GetTypes()
                .Where(t => t.IsAbstract == false);

            var customCheckTypes = serviceControlTypes.Where(t => typeof(ICustomCheck).IsAssignableFrom(t));

            foreach (var customCheckType in customCheckTypes)
            {
                var constructor = customCheckType.GetConstructors().Single();
                var constructorParameters = constructor.GetParameters()
                    .Select(p => p.ParameterType == typeof(Settings) ? settings : null)
                    .ToArray();
                var instance = (ICustomCheck)constructor.Invoke(constructorParameters);
                yield return instance;
            }
        }
    }
}