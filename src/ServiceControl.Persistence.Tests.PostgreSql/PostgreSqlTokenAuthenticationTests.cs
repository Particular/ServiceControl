namespace ServiceControl.Persistence.Tests.PostgreSql;

using System;
using Npgsql;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.PostgreSql;
using ServiceControl.PostgreSql.TokenAuthentication;

[TestFixture]
class PostgreSqlTokenAuthenticationTests
{
    [Test]
    public void Leaves_a_connection_string_without_token_keywords_untouched()
    {
        const string configured = "host=db.example.com; Username = sc ;Password='p;w';Application Name=\"ServiceControl\"";

        var settings = Settings(configured);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ConnectionString, Is.EqualTo(configured));
            Assert.That(settings.TokenAuthentication, Is.Null);
        }
    }

    [Test]
    public void Leaves_a_malformed_connection_string_without_token_keywords_for_npgsql_to_report()
    {
        const string configured = "Host=db.example.com;Username";

        var settings = Settings(configured);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ConnectionString, Is.EqualTo(configured));
            Assert.That(settings.TokenAuthentication, Is.Null);
        }
    }

    [Test]
    public void Leaves_a_value_that_only_mentions_a_keyword_untouched()
    {
        const string configured = "Host=db.example.com; Username=sc;Password='Token Authentication=AwsRdsIam';Application Name=Token Scope";

        var settings = Settings(configured);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ConnectionString, Is.EqualTo(configured));
            Assert.That(settings.TokenAuthentication, Is.Null);
        }
    }

    [Test]
    public void Names_the_given_setting_in_errors() =>
        Assert.That(Assert.Throws<InvalidOperationException>(() => PostgreSqlTokenAuthentication.Parse("Host=db.example.com;Username=sc;Token Authentication=Kerberos", "Some/Setting", out _)).Message,
            Does.StartWith("Some/Setting is invalid."));

    [Test]
    public void Removes_the_keywords_whatever_their_case()
    {
        var settings = Settings("Host=sc.postgres.database.azure.com;Username=sc;token authentication=azureentraid;TOKEN SCOPE=https://custom.example.com/.default");

        var npgsql = new NpgsqlConnectionStringBuilder(settings.ConnectionString);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ConnectionString, Does.Not.Contain("token").IgnoreCase);
            Assert.That(npgsql.Host, Is.EqualTo("sc.postgres.database.azure.com"));
            Assert.That(npgsql.Username, Is.EqualTo("sc"));
            Assert.That(settings.TokenAuthentication, Is.EqualTo(new AzureEntraIdTokenAuthentication("https://custom.example.com/.default", null)));
        }
    }

    [Test]
    public void Reads_quoted_values_and_keeps_the_others_intact()
    {
        var settings = Settings("Host=db.example.com;Username=sc;Token Authentication=\"AwsRdsIam\";Application Name=\"a;b\"");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.TokenAuthentication, Is.InstanceOf<AwsRdsIamTokenAuthentication>());
            Assert.That(new NpgsqlConnectionStringBuilder(settings.ConnectionString).ApplicationName, Is.EqualTo("a;b"));
        }
    }

    [TestCase("Kerberos")]
    [TestCase("Azure")]
    [TestCase("")]
    public void Rejects_an_unknown_token_authentication(string value)
    {
        var exception = Invalid($"Host=db.example.com;Username=sc;Token Authentication={value}");

        Assert.That(exception.Message, Does.Contain("Token Authentication").And.Contain("AzureEntraId").And.Contain("AwsRdsIam"));
    }

    [TestCase("Host=sc.postgres.database.azure.com;Username=sc;Token Scope=https://ossrdbms-aad.database.windows.net/.default")]
    [TestCase("Host=db.example.com;Username=sc;Token Authentication=AwsRdsIam;Token Scope=https://ossrdbms-aad.database.windows.net/.default")]
    [TestCase("Host=db.example.com;Username=sc;Token Scope=")]
    public void Rejects_a_token_scope_without_azure_entra_id(string connectionString) =>
        Assert.That(Invalid(connectionString).Message, Does.Contain("Token Scope").And.Contain("Token Authentication=AzureEntraId"));

    [TestCase("http://ossrdbms-aad.database.windows.net/.default")]
    [TestCase("ossrdbms-aad.database.windows.net/.default")]
    [TestCase("")]
    public void Rejects_a_token_scope_that_is_not_an_absolute_https_uri(string scope) =>
        Assert.That(Invalid($"Host=db.example.com;Username=sc;Token Authentication=AzureEntraId;Token Scope={scope}").Message, Does.Contain("Token Scope").And.Contain("https"));

    [TestCase("Password")]
    [TestCase("PWD")]
    public void Rejects_a_password_without_repeating_it(string keyword) =>
        Assert.That(Invalid($"Host=db.example.com;Username=sc;{keyword}=hunter2;Token Authentication=AwsRdsIam").Message, Does.Contain("Password").And.Not.Contain("hunter2"));

    [Test]
    public void Rejects_a_passfile() =>
        Assert.That(Invalid("Host=db.example.com;Username=sc;Passfile=/home/sc/.pgpass;Token Authentication=AwsRdsIam").Message, Does.Contain("Passfile").And.Contain("Token Authentication"));

    [TestCase("AzureEntraId")]
    [TestCase("AwsRdsIam")]
    public void Requires_a_username(string value) =>
        Assert.That(Invalid($"Host=sc.postgres.database.azure.com;Token Authentication={value}").Message, Does.Contain("Username").And.Contain("Token Authentication"));

    [TestCase("Host=10.0.0.4")]
    [TestCase("Host=sc.postgres.database.azure.com,db.example.com")]
    [TestCase("")]
    public void Requires_a_token_scope_when_the_host_names_no_known_azure_cloud(string host) =>
        Assert.That(Invalid($"{host};Username=sc;Token Authentication=AzureEntraId").Message, Does.Contain("Token Scope").And.Contain("Token Authentication=AzureEntraId"));

    [TestCase("")]
    [TestCase(";Token Scope=https://ossrdbms-aad.database.windows.net/.default")]
    public void Rejects_azure_hosts_in_different_clouds(string scope) =>
        Assert.That(Invalid($"Host=a.postgres.database.azure.com,b.postgres.database.usgovcloudapi.net;Username=sc;Token Authentication=AzureEntraId{scope}").Message, Does.Contain("Token Authentication=AzureEntraId").And.Contain("different Azure clouds"));

    [TestCase("Host=a.rds.amazonaws.com,b.rds.amazonaws.com;")]
    [TestCase("")]
    public void Requires_exactly_one_host_for_aws_rds_iam(string host) =>
        Assert.That(Invalid($"{host}Username=sc;Token Authentication=AwsRdsIam").Message, Does.Contain("Token Authentication=AwsRdsIam").And.Contain("one host"));

    [Test]
    public void Accepts_one_aws_host_with_a_port() =>
        Assert.That(Settings("Host=sc.abc.eu-west-1.rds.amazonaws.com:5433;Username=sc;Token Authentication=AwsRdsIam").TokenAuthentication, Is.InstanceOf<AwsRdsIamTokenAuthentication>());

    [TestCase("sc.postgres.database.azure.com", "https://ossrdbms-aad.database.windows.net/.default", null)]
    [TestCase("SC.Postgres.Database.Azure.COM:5432", "https://ossrdbms-aad.database.windows.net/.default", null)]
    [TestCase("sc.privatelink.postgres.database.azure.com", "https://ossrdbms-aad.database.windows.net/.default", null)]
    [TestCase("sc.postgres.database.usgovcloudapi.net", "https://ossrdbms-aad.database.usgovcloudapi.net/.default", "https://login.microsoftonline.us/")]
    [TestCase("sc.postgres.database.chinacloudapi.cn", "https://ossrdbms-aad.database.chinacloudapi.cn/.default", "https://login.chinacloudapi.cn/")]
    [TestCase("a.postgres.database.azure.com, b.postgres.database.azure.com:6432", "https://ossrdbms-aad.database.windows.net/.default", null)]
    public void Maps_the_host_to_its_azure_cloud(string host, string scope, string authorityHost) =>
        Assert.That(Settings($"Host={host};Username=sc;Token Authentication=AzureEntraId").TokenAuthentication,
            Is.EqualTo(new AzureEntraIdTokenAuthentication(scope, authorityHost is null ? null : new Uri(authorityHost))));

    [TestCase("https://ossrdbms-aad.database.usgovcloudapi.net/.default", "https://login.microsoftonline.us/")]
    [TestCase("https://ossrdbms-aad.database.chinacloudapi.cn/.default", "https://login.chinacloudapi.cn/")]
    [TestCase("https://ossrdbms-aad.database.windows.net/.default", null)]
    [TestCase("https://sovereign.example/.default", null)]
    public void Takes_the_authority_host_from_a_known_token_scope(string scope, string authorityHost) =>
        Assert.That(Settings($"Host=10.0.0.4;Username=sc;Token Authentication=AzureEntraId;Token Scope={scope}").TokenAuthentication,
            Is.EqualTo(new AzureEntraIdTokenAuthentication(scope, authorityHost is null ? null : new Uri(authorityHost))));

    [Test]
    public void Prefers_the_token_scope_over_the_host() =>
        Assert.That(Settings("Host=sc.postgres.database.azure.com;Username=sc;Token Authentication=AzureEntraId;Token Scope=https://custom.example.com/.default").TokenAuthentication,
            Is.EqualTo(new AzureEntraIdTokenAuthentication("https://custom.example.com/.default", null)));

    [TestCase("sc.postgres.database.azure.com.example.com")]
    [TestCase("postgres.database.azure.com")]
    public void Does_not_map_a_host_that_only_resembles_an_azure_one(string host) =>
        Assert.That(AzureEntraIdCloud.ForHost(host), Is.Null);

    [Test]
    public void Names_the_setting_when_the_connection_string_cannot_be_parsed() =>
        Assert.That(Assert.Throws<InvalidOperationException>(() => Settings("Host=db.example.com;Username=sc;Token Authentication=AwsRdsIam;Port")).Message, Does.StartWith("Database/ConnectionString is not a valid connection string."));

    [Test]
    public void Names_the_setting_when_npgsql_rejects_an_unknown_keyword_with_an_empty_value() =>
        Assert.That(Assert.Throws<InvalidOperationException>(() => Settings("Host=db.example.com;Username=sc;Token Authentication=AwsRdsIam;Foo=''")).Message, Does.StartWith("Database/ConnectionString is not a valid connection string."));

    [Test]
    public void Does_not_repeat_a_password_swallowed_by_a_malformed_value()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Settings("Host=db.example.com;Username=sc;Port=5432 Password=hunter2;Token Authentication=AwsRdsIam"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain("Database/ConnectionString").And.Not.Contain("hunter2"));
            Assert.That(exception.InnerException, Is.Null);
        }
    }

    static PostgreSqlPersisterSettings Settings(string connectionString) => new()
    {
        ConnectionString = connectionString,
        BodyStorage = new FileSystemBodyStorageSettings { StoragePath = "unused" }
    };

    static InvalidOperationException Invalid(string connectionString)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Settings(connectionString));

        Assert.That(exception.Message, Does.StartWith("Database/ConnectionString is invalid."));

        return exception;
    }
}
