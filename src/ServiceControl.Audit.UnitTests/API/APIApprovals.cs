namespace ServiceControl.UnitTests.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Web.Http.Controllers;
    using System.Web.Http.Hosting;
    using System.Web.Http.Routing;
    using Audit.Infrastructure;
    using Audit.Infrastructure.Settings;
    using Audit.Infrastructure.WebApi;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void PublicClr()
        {
            var publicApi = ApiGenerator.GeneratePublicApi(typeof(Bootstrapper).Assembly);
            Approver.Verify(publicApi);
        }

        [Test]
        public void RootPathValue()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost");
            request.Properties.Add(HttpPropertyKeys.RequestContextKey, new HttpRequestContext {VirtualPathRoot = "/"});

            var controller = new RootController(new LoggingSettings("testEndpoint", false), new Settings())
            {
                Url = new UrlHelper(request)
            };

            var result = controller.Urls();

            Approver.Verify(result.Content);
        }

        [Test]
        public void PlatformSampleSettings()
        {
            //HINT: Particular.PlatformSample includes a parameterized version of the ServiceControl.exe.config file.
            //If any changes have been made to settings, this may break the embedded config in that project, which may need to be updated.

            Approver.Verify(new Settings());
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