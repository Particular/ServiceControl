using System.IO;
using NUnit.Framework;
using Raven.Imports.Newtonsoft.Json;
using ServiceControl.Audit.Infrastructure.Migration;

[TestFixture]
class MigrationTests
{
    [TestCase("ServiceControl.SagaAudit.SagaInfo, ServiceControl.Audit")]
    [TestCase("ServiceControl.SagaAudit.SagaInfo, ServiceControl.SagaAudit")]
    public void VerifyMigration(string oldType)
    {
        var serializedForm = $@"{{
""$type"":""{oldType}"",
""ChangeStatus"":null,
""SagaType"":null,
""SagaId"":""00000000-0000-0000-0000-000000000000""}}";

        var serializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.All,
            Binder = new MigratedTypeAwareBinder()
        };

        serializer.Deserialize(new JsonTextReader(new StringReader(serializedForm)));

        Assert.Pass("Deserialized correctly");
    }
}