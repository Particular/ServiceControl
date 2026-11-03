namespace ServiceControl.Transport.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NUnit.Framework;
using ServiceControl.PostgreSql.TokenAuthentication;
using Transports.PostgreSql;

[TestFixture]
class PostgreSqlTokenDataSourcesTests
{
    [Test]
    public void Leaves_a_password_connection_string_to_the_transport()
    {
        const string configured = "Host=localhost; Username=sc;Password='p;w'";
        var dataSources = new PostgreSqlTokenDataSources(_ => throw new InvalidOperationException("No token provider is needed."));

        var dataSource = dataSources.GetOrCreate(configured, "Setting", out var npgsqlConnectionString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dataSource, Is.Null);
            Assert.That(npgsqlConnectionString, Is.EqualTo(configured));
        }
    }

    [Test]
    public async Task Shares_one_data_source_per_connection_string()
    {
        var dataSources = new PostgreSqlTokenDataSources(_ => new FixedTokenProvider());

        await using var first = dataSources.GetOrCreate("Host=a.example.com;Username=sc;Token Authentication=AwsRdsIam", "Setting", out var npgsqlConnectionString);
        var again = dataSources.GetOrCreate("server = a.example.com; USER ID=sc; token authentication=awsrdsiam", "Setting", out _);
        await using var other = dataSources.GetOrCreate("Host=b.example.com;Username=sc;Token Authentication=AwsRdsIam", "Setting", out _);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(again, Is.SameAs(first));
            Assert.That(other, Is.Not.SameAs(first));
            Assert.That(npgsqlConnectionString, Does.Not.Contain("Token").IgnoreCase);
        }
    }

    sealed class FixedTokenProvider : IPostgreSqlTokenProvider
    {
        public ValueTask<string> GetToken(NpgsqlConnectionStringBuilder settings, CancellationToken cancellationToken = default) => ValueTask.FromResult("token");
    }
}
