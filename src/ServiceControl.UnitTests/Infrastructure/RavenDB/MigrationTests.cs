using System.IO;
using NUnit.Framework;
using Raven.Imports.Newtonsoft.Json;
using ServiceControl.Infrastructure.RavenDB;

[TestFixture]
class MigrationTests
{
    [Test]
    public void VerifySagaInfoMigration()
    {
        var serializedForm = @"{
""$type"":""ServiceControl.SagaAudit.SagaInfo, ServiceControl"",
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

    [Test]
    public void VerifySagaEventLogMigration()
    {
        var serializedForm = @"{
""$type"":""ServiceControl.EventLog.EventLogItem, ServiceControl""}";

        var serializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.All,
            Binder = new MigratedTypeAwareBinder()
        };

        serializer.Deserialize(new JsonTextReader(new StringReader(serializedForm)));

        Assert.Pass("Deserialized correctly");
    }

    [Test]
    public void VerifyCustomCheckMigration()
    {
        var serializedForm = @"{
""$type"":""ServiceControl.CustomChecks.CustomCheck, ServiceControl""}";

        var serializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.All,
            Binder = new MigratedTypeAwareBinder()
        };

        serializer.Deserialize(new JsonTextReader(new StringReader(serializedForm)));

        Assert.Pass("Deserialized correctly");
    }

    [Test]
    public void VerifyNotificationSettingsMigration()
    {
        var serializedForm = @"{
""$type"":""ServiceControl.Notifications.NotificationsSettings, ServiceControl""}";

        var serializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.All,
            Binder = new MigratedTypeAwareBinder()
        };

        serializer.Deserialize(new JsonTextReader(new StringReader(serializedForm)));

        Assert.Pass("Deserialized correctly");
    }
}