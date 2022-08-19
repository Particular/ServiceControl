namespace ServiceControl.UnitTests
{
    using System;
    using System.Linq;
    using Audit.Infrastructure;
    using NUnit.Framework;
    using Particular.Approvals;
    using Raven.Client.Indexes;
    using ServiceControl.Audit.Infrastructure.Settings;

    class RavenPersistedTypes
    {
        [Test]
        public void Verify()
        {
            var documentTypes = typeof(Bootstrapper).Assembly.GetTypes()
                .Where(type => typeof(AbstractIndexCreationTask).IsAssignableFrom(type))
                .SelectMany(indexType => indexType.BaseType?.GenericTypeArguments)
                .Distinct();

            var ravenPersistenceType = Type.GetType(DataStoreConfig.RavenDbPersistenceTypeFullyQualifiedName, true);

            var ravenPersistenceTypes = ravenPersistenceType.Assembly.GetTypes()
                .Where(type => typeof(AbstractIndexCreationTask).IsAssignableFrom(type))
                .SelectMany(indexType => indexType.BaseType?.GenericTypeArguments)
                .Distinct();

            var documentTypeNames = string.Join(Environment.NewLine, ravenPersistenceTypes.Select(t => t.AssemblyQualifiedName).Union(documentTypes.Select(t => t.AssemblyQualifiedName)));

            Approver.Verify(documentTypeNames);
        }
    }
}