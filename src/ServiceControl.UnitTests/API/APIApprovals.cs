namespace ServiceControl.UnitTests.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Routing;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Particular.Approvals;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Licensing;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.WebApi;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void RootPathValue()
        {
            var httpContext = new DefaultHttpContext { Request = { PathBase = "http://localhost" } };
            var actionContext = new ActionContext { HttpContext = httpContext };

            var controller = new RootController(
                new ActiveLicense { IsValid = true },
                new LoggingSettings("testEndpoint"),
                new Settings(),
                httpClientFactory: null
                )
            {
                Url = new UrlHelper(actionContext)
            };

            var result = controller.Urls();

            Approver.Verify(result);
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
                           .SelectMany(att => att.HttpMethods.Select(m => m))
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

        IEnumerable<(MethodInfo Method, IRouteTemplateProvider Route)> GetControllerRoutes()
        {
            var controllers = typeof(Program).Assembly.GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t));

            foreach (var type in controllers)
            {
                foreach (var methodInfo in type.GetMethods())
                {
                    var routeAtts = methodInfo.GetCustomAttributes(true).OfType<IRouteTemplateProvider>();
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
            var settings = new Settings
            {
                LicenseFileText = null
            };

            Approver.Verify(settings, RemoveDataStoreSettings);
        }

        string RemoveDataStoreSettings(string json)
        {
            var allLines = json.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            var settingsLines = allLines.AsSpan(1, allLines.Length - 2);

            var result = string.Empty;

            var dataStoreSettings = new[] { nameof(Settings.PersistenceType) };

            foreach (var settingLine in settingsLines)
            {
                var parts = settingLine.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                var settingName = parts[0].Trim('"', ' ');

                if (dataStoreSettings.Contains(settingName) == false)
                {
                    result += settingLine + Environment.NewLine;
                }
            }

            return $"{{\r\n{result}}}";
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

            var serviceControlTypes = typeof(WebApplicationBuilderExtension).Assembly
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