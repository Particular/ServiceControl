using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using ServiceControl.Infrastructure.RavenDB;

[TestFixture]
class MigrationTests
{
    [Test]
    public void VerifyMigration()
    {
        var serializedForm = @"{
""$type"":""ServiceControl.SagaAudit.SagaInfo, ServiceControl"",
""ChangeStatus"":null,
""SagaType"":null,
""SagaId"":""00000000-0000-0000-0000-000000000000""}";

        var serializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.All,
            SerializationBinder = new MigratedTypeAwareBinder()
        };

        serializer.Deserialize(new JsonTextReader(new StringReader(serializedForm)));

        Assert.Pass("Deserialized correctly");
    }
}