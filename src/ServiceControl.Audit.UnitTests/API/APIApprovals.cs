namespace ServiceControl.UnitTests.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Text;
    using System.Web.Http.Controllers;
    using System.Web.Http.Hosting;
    using System.Web.Http.Routing;
    using Audit;
    using Audit.Infrastructure;
    using Audit.Infrastructure.Settings;
    using Audit.Infrastructure.WebApi;
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;
    using ServiceControl.Audit.Persistence.InMemory;
    using ServiceControl.Transports.Learning;

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
        public void RootPathValue()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost");
            request.Properties.Add(HttpPropertyKeys.RequestContextKey, new HttpRequestContext { VirtualPathRoot = "/" });

            var settings = CreateTestSettings();

            var controller = new RootController(new LoggingSettings("testEndpoint"), settings)
            {
                Url = new UrlHelper(request)
            };

            var result = controller.Urls();

            Approver.Verify(result.Content);
        }

        [Test]
        public void HttpApiRoutes()
        {
            var httpApiMethods = GetControllerRoutes()
                .Select(pair =>
                {
                    var type = pair.Method.DeclaringType;
                    var httpMethods = pair.Method.GetCustomAttributes(true)
                        .OfType<IActionHttpMethodProvider>()
                           .SelectMany(att => att.HttpMethods.Select(m => m.Method))
                           .Distinct()
                           .OrderBy(httpMethod => httpMethod);

                    if (!httpMethods.Any())
                    {
                        throw new Exception($"Method {type.FullName}:{pair.Method.Name} has Route attribute but no method attribute like HttpGet.");
                    }

                    var parametersString = string.Join(", ", pair.Method.GetParameters().Select(p => $"{PrettyTypeName(p.ParameterType)} {p.Name}"));
                    var methodSignature = $"{type.FullName}:{pair.Method.Name}({parametersString})";

                    return new
                    {
                        MethodSignature = methodSignature,
                        HttpMethods = string.Join("/", httpMethods),
                        Route = pair.Route.Template
                    };
                })
                .OrderBy(x => x.Route).ThenBy(x => x.HttpMethods)
                .ToArray();

            var builder = new StringBuilder();
            foreach (var item in httpApiMethods)
            {
                builder.AppendLine($"{item.HttpMethods} /{item.Route} => {item.MethodSignature}");
            }
            var httpApi = builder.ToString();
            Console.Write(httpApi);

            Approver.Verify(httpApi);
        }

        IEnumerable<(MethodInfo Method, IHttpRouteInfoProvider Route)> GetControllerRoutes()
        {
            var controllers = typeof(Program).Assembly.GetTypes()
                .Where(t => typeof(IHttpController).IsAssignableFrom(t));

            foreach (var type in controllers)
            {
                foreach (var methodInfo in type.GetMethods())
                {
                    var routeAtts = methodInfo.GetCustomAttributes(true).OfType<IHttpRouteInfoProvider>();
                    foreach (var routeAtt in routeAtts)
                    {
                        yield return (methodInfo, routeAtt);
                    }
                }
            }
        }

        static string PrettyTypeName(Type t)
        {
            if (t.IsArray)
            {
                return PrettyTypeName(t.GetElementType()) + "[]";
            }

            if (t.IsGenericType)
            {
                return string.Format("{0}<{1}>",
                    t.Name.Substring(0, t.Name.LastIndexOf("`", StringComparison.InvariantCulture)),
                    string.Join(", ", t.GetGenericArguments().Select(PrettyTypeName)));
            }

            return t.Name;
        }

        [Test]
        public void PlatformSampleSettings()
        {
            //HINT: Particular.PlatformSample includes a parameterized version of the ServiceControl.exe.config file.
            //If any changes have been made to settings, this may break the embedded config in that project, which may need to be updated.
            var settings = CreateTestSettings();

            settings.LicenseFileText = null;

            Approver.Verify(settings);
        }

        static Settings CreateTestSettings()
        {
            return new Settings(
                Settings.DEFAULT_SERVICE_NAME,
                typeof(LearningTransportCustomization).AssemblyQualifiedName,
                typeof(InMemoryPersistence).AssemblyQualifiedName);
        }
    }
}