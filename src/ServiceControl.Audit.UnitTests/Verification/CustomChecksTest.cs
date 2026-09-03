namespace ServiceControl.Audit.UnitTests.API
{
    using System;
    using System.Linq;
    using Audit.Infrastructure.Settings;
    using Contracts.CustomChecks;
    using NUnit.Framework;
    using NServiceBus.CustomChecks;
    using Particular.Approvals;

    [TestFixture]
    class CustomChecksTest
    {
        // Mirrors the primary's InternalCustomCheckClassification audit section (string literals — the audit
        // assembly is not referenced by the primary). Adding a custom check to the audit instance MUST be
        // accompanied by an entry in the primary registry; this snapshot makes that visible. The audit
        // RavenDB persister checks (CheckDirtyMemory, CheckFreeDiskSpace, CheckRavenDBIndexLag) are runtime
        // plugins not referenced here, so they are covered by the persistence approval tests instead
        [Test]
        public void VerifyCustomChecks()
        {
            var settings = (object)new Settings("LearningTransport", "InMemory");

            var discovered =
                from type in typeof(Settings).Assembly.GetTypes()
                where type is { IsAbstract: false, IsInterface: false }
                   && typeof(ICustomCheck).IsAssignableFrom(type)
                let constructor = type.GetConstructors().Single()
                let constructorParameters = constructor.GetParameters()
                    .Select(p => p.ParameterType == typeof(Settings) ? settings : null)
                    .ToArray()
                let instance = (ICustomCheck)constructor.Invoke(constructorParameters)
                let classified = InternalCustomCheckClassification.IsInternal(instance.Id)
                orderby instance.Category, instance.Id
                select $"{instance.Category}: {instance.Id} => {(classified ? "internal" : "MISSING FROM REGISTRY")}";

            Approver.Verify(string.Join(Environment.NewLine, discovered));
        }
    }
}