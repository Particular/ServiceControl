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
""$type"":""ServiceControl.WhateverNamespace.CustomCheck, ServiceControl"",
""Id"":""SomeId"",
""CustomCheckId"":""CustomCheckId"",
""Category"":""SomeCategory""}";

        var serializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.All,
            SerializationBinder = new MigratedTypeAwareBinder()
        };

        serializer.Deserialize(new JsonTextReader(new StringReader(serializedForm)));

        Assert.Pass("Deserialized correctly");
    }
}