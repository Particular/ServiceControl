namespace ServiceControl.UnitTests.API
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using NServiceBus.CustomChecks;
    using Particular.Approvals;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.CustomChecks;

    [TestFixture]
    class InternalCustomCheckRegistryApprovals
    {
        [Test]
        public void Every_shipped_check_in_the_app_assembly_is_in_the_registry()
        {
            // HINT: The primary references persister and transport assemblies as runtime-loaded plugins
            // (ReferenceOutputAssembly="false" Private="false"), so only checks compiled into the
            // ServiceControl app assembly are visible here. The persister/transport checks are covered
            // by the runtime-assembly-scan acceptance test (see .plans/internal-customchecks.md §7.6).
            var settings = (object)new Settings();

            var discovered =
                from type in typeof(Settings).Assembly.GetTypes()
                where type is { IsAbstract: false, IsInterface: false }
                   && typeof(ICustomCheck).IsAssignableFrom(type)
                let constructor = type.GetConstructors().Single()
                let constructorParameters = constructor.GetParameters()
                    .Select(p => p.ParameterType == typeof(Settings) ? settings : null)
                    .ToArray()
                let instance = (ICustomCheck)constructor.Invoke(constructorParameters)
                let severity = InternalCustomCheckClassification.SeverityFor(instance.Id)
                orderby instance.Category, instance.Id
                select $"{instance.Category}: {instance.Id} => {severity?.ToString() ?? "MISSING FROM REGISTRY"}";

            Approver.Verify(string.Join(Environment.NewLine, discovered));
        }
    }
}