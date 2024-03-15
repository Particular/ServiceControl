namespace ServiceControl.UnitTests
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using Particular.Approvals;
    using Raven.Client.Documents.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;

    class RavenPersistedTypes
    {
        [Test]
        public void Verify()
        {
            var allTypes = typeof(Settings).Assembly.GetTypes().Concat(typeof(RavenQueryExtensions).Assembly.GetTypes()).Concat(typeof(EndpointsView).Assembly.GetTypes());

            var documentTypes = allTypes
                .Where(type => typeof(AbstractIndexCreationTask).IsAssignableFrom(type))
                .SelectMany(indexType => indexType.BaseType?.GenericTypeArguments)
                .Distinct();

            var documentTypeNames = string.Join(Environment.NewLine, documentTypes.Select(t => t.AssemblyQualifiedName).OrderBy(x => x));

            Approver.Verify(documentTypeNames);
        }
    }
}