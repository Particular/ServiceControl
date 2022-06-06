namespace ServiceControl.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using CustomChecks.Internal;
    using EventLog;
    using NUnit.Framework;
    using Particular.Approvals;
    using Particular.ServiceControl;
    using Raven.Client.Indexes;
    using ServiceControl.ExternalIntegrations;
    using ServiceControl.Notifications.Email;

    class RavenPersistedTypes
    {
        [Test]
        public void Verify()
        {
            var assemblies = new[]
            {
                typeof(Bootstrapper).Assembly,
                typeof(CustomChecksHostBuilderExtensions).Assembly,
                typeof(EventLogHostBuilderExtensions).Assembly,
                typeof(ExternalIntegrationsHostBuilderExtensions).Assembly,
                typeof(EmailNotificationHostBuilderExtensions).Assembly,
            };

            var indexedTypes = assemblies.SelectMany(x => x.GetTypes())
                .Where(x => typeof(AbstractIndexCreationTask).IsAssignableFrom(x))
                .SelectMany(indexType => indexType.BaseType?.GenericTypeArguments)
                .Distinct();


            var singleDocumentTypes = assemblies.SelectMany(x => x.GetTypes()).Where(x => GetStaticFields(x).Any(f => f.Name == "SingleDocumentId"));

            var documentTypeNames = string.Join(Environment.NewLine, indexedTypes.Concat(singleDocumentTypes).Select(t => t.AssemblyQualifiedName).OrderBy(x => x));

            Approver.Verify(documentTypeNames);
        }

        static FieldInfo[] GetStaticFields(Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        }
    }
}