namespace ServiceControl.Persistence.Tests;

using System;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;

[TestFixture]
class SchemaNameTests
{
    [TestCase("dbo")]
    [TestCase("public")]
    [TestCase("_private")]
    [TestCase("ServiceControl")]
    [TestCase("sc_test_9d0d0a1d3d4b4d1ab3f1a1b2c3d4e5f6")]
    public void Accepts(string schema) => Assert.That(SchemaName.Validate(schema), Is.EqualTo(schema));

    [TestCase("", Description = "empty")]
    [TestCase("   ", Description = "whitespace")]
    [TestCase("1schema", Description = "leading digit")]
    [TestCase("my schema", Description = "space")]
    [TestCase("my-schema", Description = "hyphen")]
    [TestCase("my.schema", Description = "dot")]
    [TestCase("\"quoted\"", Description = "quotes")]
    [TestCase("sc]; DROP TABLE FailedMessages--", Description = "injection through a closing bracket")]
    [TestCase("sc'; DROP TABLE FailedMessages--", Description = "injection through a closing quote")]
    public void Rejects(string schema) => Assert.Throws<ArgumentException>(() => SchemaName.Validate(schema));

    [Test]
    public void Rejects_a_name_longer_than_PostgreSql_allows() =>
        Assert.Throws<ArgumentException>(() => SchemaName.Validate(new string('a', SchemaName.MaxLength + 1)));

    [Test]
    public void Accepts_a_name_at_the_limit() =>
        Assert.DoesNotThrow(() => SchemaName.Validate(new string('a', SchemaName.MaxLength)));
}
