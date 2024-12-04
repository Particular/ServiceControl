namespace ServiceControl.Transport.Tests;

using NUnit.Framework;
using Transports.PostgreSql;

[TestFixture]
class ConnectionStringExtensionsTests : TransportTestFixture
{
    [TestCase("table")]
    [TestCase("schema.table")]
    [TestCase("schema.my.table")]
    public void ShouldParseSchemaFromSubscriptionTable(string customSubscriptionTableContainingSchema)
    {
        string connectionString = $"{configuration.ConnectionString};Subscriptions Table={customSubscriptionTableContainingSchema}";

        _ = connectionString.RemoveCustomConnectionStringParts(out var _, out var subscriptionsTableSetting);
        var subscriptionsAddress = QueueAddress.Parse(subscriptionsTableSetting);

        Assert.That(subscriptionsAddress.Table, Is.Not.Null);

        if (customSubscriptionTableContainingSchema.Contains("."))
        {
            Assert.That(subscriptionsAddress.Schema, Is.Not.Null);
            Assert.That(subscriptionsAddress.Table, Is.EqualTo(customSubscriptionTableContainingSchema.Substring(customSubscriptionTableContainingSchema.IndexOf(".") + 1)));
            Assert.That(subscriptionsAddress.Schema, Is.EqualTo(customSubscriptionTableContainingSchema.Substring(0, customSubscriptionTableContainingSchema.IndexOf("."))));
        }
        else
        {
            Assert.That(subscriptionsAddress.Schema, Is.Null);
            Assert.That(subscriptionsAddress.Table, Is.EqualTo(customSubscriptionTableContainingSchema));
        }
    }

    [TestCase("\"table\"")]
    [TestCase("\"schema.table\"")]
    [TestCase("\"schema.my.table\"")]
    public void ShouldParseOnlyTableFromSubscriptionTableWhenEnclosedInQuotes(string customSubscriptionTableWithoutSchema)
    {
        string connectionString = $"{configuration.ConnectionString};Subscriptions Table={customSubscriptionTableWithoutSchema}";

        _ = connectionString.RemoveCustomConnectionStringParts(out var _, out var subscriptionsTableSetting);
        var subscriptionsAddress = QueueAddress.Parse(subscriptionsTableSetting);

        Assert.That(subscriptionsAddress.Schema, Is.Null);
        Assert.That(subscriptionsAddress.Table, Is.Not.Null);
        Assert.That(subscriptionsAddress.Table, Is.EqualTo(PostgreSqlNameHelper.Unquote(customSubscriptionTableWithoutSchema)));
    }
}