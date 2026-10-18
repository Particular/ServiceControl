namespace ServiceControl.Federation.UnitTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Api;
using Members;
using NUnit.Framework;

/// <summary>
/// Every route a ServiceControl instance exposes must have a federation strategy. The approved route
/// lists are the instances' own approval files, so adding a route to ServiceControl fails this test
/// until the route is classified in <see cref="PrimaryRoutes"/> or <see cref="MonitoringRoutes"/>.
/// </summary>
[TestFixture]
class RouteCoverageTests
{
    [Test]
    public void Every_primary_route_has_a_strategy()
    {
        var approved = ApprovedRoutes("Primary.txt", PrimaryPrefix)
            .Concat(ApprovedRoutes("Licensing.txt", _ => "licensing/"));

        AssertCovered(approved, PrimaryRoutes.All);
    }

    [Test]
    public void Every_monitoring_route_has_a_strategy() =>
        AssertCovered(ApprovedRoutes("Monitoring.txt", MonitoringPrefix), MonitoringRoutes.All);

    [Test]
    public void Route_tables_have_no_duplicates()
    {
        var duplicates = FederationHostingExtensions.AllRoutes
            .SelectMany(route => route.Methods.Select(method => $"{route.Surface} {method} {Normalise(route.Template)}"))
            .GroupBy(key => key)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.That(duplicates, Is.Empty);
    }

    static void AssertCovered(IEnumerable<string> approved, IEnumerable<FederatedRoute> routes)
    {
        var proxied = routes
            .SelectMany(route => route.Methods.Select(method => $"{method} {Normalise(route.Template)}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = approved.Where(route => !proxied.Contains(route)).ToList();

        Assert.That(missing, Is.Empty, "These routes have no federation strategy. Add them to the route table.");
    }

    // The approval files print only the method-level template; these controllers add a prefix.
    static string PrimaryPrefix(string controller) => controller switch
    {
        _ when controller.EndsWith("AuthenticationController", StringComparison.Ordinal) => "authentication/",
        _ when controller.EndsWith("SystemMaintenanceController", StringComparison.Ordinal) => "maintenance/",
        _ => ""
    };

    static string MonitoringPrefix(string controller) =>
        controller.EndsWith("MyRoutesController", StringComparison.Ordinal) ? "api/" : "";

    static IEnumerable<string> ApprovedRoutes(string file, Func<string, string> prefixFor)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "ApprovedRoutes", file);
        foreach (var line in File.ReadAllLines(path).Where(l => l.Contains("=>", StringComparison.Ordinal)))
        {
            var match = RouteLine().Match(line);
            Assert.That(match.Success, Is.True, $"Unexpected approval line: {line}");

            var controller = match.Groups["controller"].Value;
            var template = prefixFor(controller) + match.Groups["template"].Value.TrimStart('/');
            foreach (var method in match.Groups["methods"].Value.Split('/'))
            {
                yield return $"{method} {Normalise(template)}";
            }
        }
    }

    static string Normalise(string template) =>
        string.Join('/', template.Trim('/').Split('/').Select(segment => Parameter().Replace(segment, "{}"))).ToLowerInvariant();

    static Regex RouteLine() => new(@"^(?<methods>[A-Z/]+) (?<template>\S+) => (?<controller>[^:]+):");

    static Regex Parameter() => new(@"\{[^}]*\}");
}
