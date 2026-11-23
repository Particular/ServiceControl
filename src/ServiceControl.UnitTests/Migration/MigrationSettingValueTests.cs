namespace ServiceControl.UnitTests.Migration;

using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class MigrationSettingValueTests
{
    [TestCase("Host=pg;Database=sc;Password=secret", "secret", "Host=pg")]
    [TestCase("Server=sql;User ID=sa;PWD=secret", "secret", "Server=sql")]
    [TestCase("Host=pg;Username=sc;PSW=secret", "secret", "Host=pg")]
    [TestCase("DefaultEndpointsProtocol=https;AccountName=a;AccountKey=secret", "secret", "AccountName=a")]
    [TestCase("Endpoint=sb://a/;Access Token=secret", "secret", "Endpoint=sb")]
    [TestCase("Endpoint=sb://a/;SharedAccessSignature=secret", "secret", "Endpoint=sb")]
    public void MaskSecrets_hides_every_secret_value_and_keeps_the_rest(string connectionString, string secret, string kept) =>
        Assert.That(MigrationSettingValue.MaskSecrets(connectionString), Does.Contain("*****").And.Not.Contain(secret).And.Contain(kept).IgnoreCase);

    [Test]
    public void MaskSecrets_hides_a_password_that_holds_a_semicolon() =>
        Assert.That(MigrationSettingValue.MaskSecrets("Host=pg;Password='se;cret';Database=sc"), Does.Contain("*****").And.Not.Contain("cret").And.Contain("Database=sc").IgnoreCase);

    [Test]
    public void MaskSecrets_shows_no_part_of_a_string_that_does_not_parse() =>
        Assert.That(MigrationSettingValue.MaskSecrets("Host=pg;Password='abc;Database=sc"), Does.Contain("*****").And.Not.Contain("abc"));
}
