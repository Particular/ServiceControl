namespace ServiceControl.UnitTests
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using Particular.Approvals;
    using Raven.Client.Indexes;
    using ServiceControl.Audit.Persistence.RavenDb;

    class RavenPersistedTypes
    {
        [Test]
        public void Verify()
        {
            var ravenPersistenceType = typeof(RavenDbPersistenceConfiguration);

            var ravenPersistenceTypes = ravenPersistenceType.Assembly.GetTypes()
                .Where(type => typeof(AbstractIndexCreationTask).IsAssignableFrom(type))
                .SelectMany(indexType => indexType.BaseType?.GenericTypeArguments)
                .Distinct();

            var documentTypeNames = string.Join(Environment.NewLine, ravenPersistenceTypes.Select(t => t.AssemblyQualifiedName));

            Approver.Verify(documentTypeNames);
        }
    }
}