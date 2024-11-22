namespace ServiceControl.Config.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;

    public class TestTheseTransportsAttribute : TestCaseSourceAttribute
    {
        public TestTheseTransportsAttribute(params string[] transportNames)
            : base(typeof(TransportTestCaseGenerator), nameof(TransportTestCaseGenerator.GetTestCases), new object[] { false, transportNames, null })
        {
        }

        public TestTheseTransportsAttribute(string[] transportNames, string[] skipTheseExplicitTransportNames)
            : base(typeof(TransportTestCaseGenerator), nameof(TransportTestCaseGenerator.GetTestCases), new object[] { false, transportNames, skipTheseExplicitTransportNames })
        {
        }
    }

    public class TestAllTransportsExceptAttribute : TestCaseSourceAttribute
    {
        public TestAllTransportsExceptAttribute(params string[] transportNames)
            : base(typeof(TransportTestCaseGenerator), nameof(TransportTestCaseGenerator.GetTestCases), new object[] { true, transportNames, null })
        {
        }
    }

    public static class TransportTestCaseGenerator
    {
        public static object[] GetTestCases(bool invertList, string[] transportNames, string[] skipTransportNames = null)
        {
            var matching = Enumerate(transportNames)
                .Where(name => skipTransportNames == null || !skipTransportNames.Contains(name))
                .ToArray();

            if (invertList)
            {
                var set = matching.ToHashSet();

                matching = ServiceControlCoreTransports.GetAllTransports()
                    .Where(manifest => !set.Contains(manifest.Name))
                    .Select(manifest => manifest.Name)
                    .ToArray();
            }

            return matching
                .Select(name => new object[] { name })
                .ToArray();
        }

        static IEnumerable<string> Enumerate(string[] transportNames)
        {
            foreach (string transportName in transportNames)
            {
                var matching = ServiceControlCoreTransports.GetAllTransports()
                    .Where(t => t.Name == transportName || t.ZipName == transportName);

                if (matching.Any())
                {
                    foreach (var match in matching)
                    {
                        yield return match.Name;
                    }
                }
                else
                {
                    throw new Exception($"Transport name '{transportName}' does not match to a transport in any detected manifest file.");
                }
            }
        }
    }
}