namespace Particular.LicensingComponent.UnitTests.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Routing;
    using NUnit.Framework;
    using Particular.Approvals;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void HttpApiRoutes()
        {
            var httpApiMethods = GetControllerRoutes()
                .Select(pair =>
                {
                    (MethodInfo method, RouteAttribute route) = pair;
                    var type = method.DeclaringType;
                    var httpMethods = method.GetCustomAttributes(true)
                        .OfType<IActionHttpMethodProvider>()
                           .SelectMany(att => att.HttpMethods.Select(m => m))
                           .Distinct()
                           .OrderBy(httpMethod => httpMethod)
                        .ToArray();

                    if (!httpMethods.Any())
                    {
                        throw new Exception($"Method {type.FullName}:{method.Name} has Route attribute but no method attribute like HttpGet.");
                    }

                    var parametersString = string.Join(", ", method.GetParameters().Select(p => $"{PrettyTypeName(p.ParameterType)} {p.Name}"));
                    var methodSignature = $"{type.FullName}:{method.Name}({parametersString})";

                    return new
                    {
                        MethodSignature = methodSignature,
                        HttpMethods = string.Join('/', httpMethods),
                        Route = route.Template
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

        IEnumerable<(MethodInfo Method, RouteAttribute Route)> GetControllerRoutes()
        {
            var controllers = typeof(ThroughputCollector).Assembly.GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t));

            foreach (var type in controllers)
            {
                foreach (var method in type.GetMethods())
                {
                    var routeAtts = method.GetCustomAttributes(true).OfType<RouteAttribute>();
                    foreach (var routeAtt in routeAtts)
                    {
                        yield return (method, routeAtt);
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
    }
}