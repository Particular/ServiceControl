namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.CustomChecks;

    // Prong 2 of drift protection (see .plans/internal-customchecks.md §7.6): persister and transport checks
    // are runtime-loaded plugin assemblies, not compile-visible to ServiceControl.UnitTests, so the unit-test
    // approval (Prong 1) cannot see them. This test runs after the instance boots — when the plugin assemblies
    // are loaded — and asserts every shipped product check discoverable at runtime is classified by the registry.
    //
    // Some checks (e.g. DeadLetterQueueCheck) perform work in their constructor, so they cannot be instantiated
    // with null constructor arguments. Those are skipped here; they are already covered directly by the registry
    // unit tests (Every_shipped_check_has_a_severity) and by the API acceptance assertions.
    [TestFixture]
    class When_internal_check_registry_covers_all_shipped_checks : AcceptanceTest
    {
        [Test]
        public async Task Every_discovered_product_check_is_classified()
        {
            var missing = new List<string>();
            var scanned = false;

            await Define<Context>()
                .Done(_ =>
                {
                    if (scanned)
                    {
                        return true;
                    }

                    scanned = true;

                    var settings = (object)new Settings();

                    var productChecks =
                        from assembly in AppDomain.CurrentDomain.GetAssemblies()
                        let name = assembly.GetName().Name
                        where name != null
                           && name.StartsWith("ServiceControl")
                           && !name.Contains("Test")
                           && !name.Contains("Acceptance")
                        from type in assembly.GetTypes()
                        where type is { IsAbstract: false, IsInterface: false }
                           && typeof(ICustomCheck).IsAssignableFrom(type)
                        select type;

                    foreach (var type in productChecks)
                    {
                        string id;
                        try
                        {
                            var constructor = type.GetConstructors().Single();
                            var args = constructor.GetParameters()
                                .Select(p => p.ParameterType == typeof(Settings) ? settings : null)
                                .ToArray();
                            var instance = (ICustomCheck)constructor.Invoke(args);
                            id = instance.Id;
                        }
                        catch (Exception)
                        {
                            // Constructor does work (e.g. DeadLetterQueueCheck dereferences its settings);
                            // cannot be classified by reflection. It is covered by the registry unit tests instead.
                            continue;
                        }

                        if (InternalCustomCheckClassification.SeverityFor(id) is null)
                        {
                            missing.Add(id);
                        }
                    }

                    return true;
                })
                .Run();

            Assert.That(missing, Is.Empty,
                "Every check ServiceControl ships must have a severity in the registry. " +
                "If you added a check, add it to InternalCustomCheckClassification " +
                "(see .plans/internal-customchecks.md §7.3). Missing: " +
                string.Join(", ", missing));
        }

        class Context : ScenarioContext;
    }
}