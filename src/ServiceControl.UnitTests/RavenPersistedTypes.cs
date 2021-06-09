namespace ServiceControl.UnitTests
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using Particular.Approvals;
    using Particular.ServiceControl;
    using Raven.Client.Indexes;

    class RavenPersistedTypes
    {
        [Test]
        public void Verify()
        {
            var documentTypes = typeof(Bootstrapper).Assembly.GetTypes()
                .Where(type => typeof(AbstractIndexCreationTask).IsAssignableFrom(type))
                .SelectMany(indexType => indexType.BaseType?.GenericTypeArguments)
                .Distinct();

            var documentTypeNames = string.Join(Environment.NewLine, documentTypes.Select(t => t.AssemblyQualifiedName));

            Approver.Verify(documentTypeNames);
        }
    }
}