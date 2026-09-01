namespace ServiceControl.Audit.UnitTests.API
{
    using System;
    using System.Linq;
    using Audit.Infrastructure.Settings;
    using NUnit.Framework;
    using NServiceBus.CustomChecks;
    using Particular.Approvals;

    [TestFixture]
    class AuditCustomCheckApprovals
    {
        // Mirrors the primary's InternalCustomCheckClassification audit section (string literals — the audit
        // assembly is not referenced by the primary). Adding a custom check to the audit instance MUST be
        // accompanied by an entry in the primary registry; this snapshot makes that visible. The audit
        // RavenDB persister checks (CheckDirtyMemory, CheckFreeDiskSpace, CheckRavenDBIndexLag) are runtime
        // plugins not referenced here, so they are covered by the multi-instance acceptance test instead
        [Test]
        public void Audit_check_ids_are_snapshot()
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
                orderby instance.Category, instance.Id
                select $"{instance.Category}: {instance.Id}";

            Approver.Verify(string.Join(Environment.NewLine, discovered));
        }
    }
}