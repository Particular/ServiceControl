namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.CustomChecks;

    // Some checks (e.g. DeadLetterQueueCheck) perform work in their constructor, so they cannot be instantiated
    // with null constructor arguments. Those are skipped here; they are already covered directly by the registry
    // unit tests (Every_shipped_check_is_internal) and by the API acceptance assertions.
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

                        if (!InternalCustomCheckClassification.IsInternal(id))
                        {
                            missing.Add(id);
                        }
                    }

                    return true;
                })
                .Run();

            Assert.That(missing, Is.Empty,
                "Every check ServiceControl ships must be in the internal registry. " +
                "If you added a check, add it to InternalCustomCheckClassification. Missing: " +
                string.Join(", ", missing));
        }

        class Context : ScenarioContext;
    }
}