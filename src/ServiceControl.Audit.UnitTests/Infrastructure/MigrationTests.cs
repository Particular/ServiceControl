using System.IO;
using NUnit.Framework;
using Raven.Imports.Newtonsoft.Json;
using ServiceControl.Audit.Infrastructure.Migration;

[TestFixture]
class MigrationTests
{
    [Test]
    public void VerifyMigration()
    {
        var serializedForm = @"{
""$type"":""ServiceControl.SagaAudit.SagaInfo, ServiceControl.Audit"",
""ChangeStatus"":null,
""SagaType"":null,
""SagaId"":""00000000-0000-0000-0000-000000000000""}";

        var serializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.All,
            Binder = new MigratedTypeAwareBinder()
        };

        serializer.Deserialize(new JsonTextReader(new StringReader(serializedForm)));

        Assert.Pass("Deserialized correctly");
    }
}