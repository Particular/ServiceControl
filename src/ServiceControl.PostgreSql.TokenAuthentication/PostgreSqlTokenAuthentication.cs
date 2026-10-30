#nullable enable
namespace ServiceControl.PostgreSql.TokenAuthentication;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Npgsql;

abstract record PostgreSqlTokenAuthentication
{
    public const string AuthenticationKeyword = "Token Authentication";
    public const string ScopeKeyword = "Token Scope";
    public const string AzureEntraId = "AzureEntraId";
    public const string AwsRdsIam = "AwsRdsIam";

    public abstract IPostgreSqlTokenProvider CreateTokenProvider();

    /// <summary>
    /// Returns null, and the connection string as it is, when neither keyword appears in it. Otherwise removes the keywords and validates them.
    /// </summary>
    /// <param name="setting">Names the connection string in error messages.</param>
    public static PostgreSqlTokenAuthentication? Parse(string connectionString, string setting, out string npgsqlConnectionString)
    {
        npgsqlConnectionString = connectionString;

        if (!connectionString.Contains(AuthenticationKeyword, StringComparison.OrdinalIgnoreCase) && !connectionString.Contains(ScopeKeyword, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var builder = CreateBuilder(connectionString, setting);
        var hasAuthentication = builder.TryGetValue(AuthenticationKeyword, out var authentication) || builder.EmptyKeywords.Contains(AuthenticationKeyword);
        var hasScope = builder.TryGetValue(ScopeKeyword, out var scope) || builder.EmptyKeywords.Contains(ScopeKeyword);

        if (!hasAuthentication && !hasScope)
        {
            return null;
        }

        builder.Remove(AuthenticationKeyword);
        builder.Remove(ScopeKeyword);
        npgsqlConnectionString = builder.ConnectionString;

        var isAzure = hasAuthentication && IsAzureEntraId(authentication as string, setting);

        if (hasScope && !isAzure)
        {
            throw Invalid(setting, $"{ScopeKeyword} is only valid together with {AuthenticationKeyword}={AzureEntraId}.");
        }

        var scopeOverride = hasScope ? ReadScope(scope as string, setting) : null;
        var npgsql = CreateNpgsqlBuilder(npgsqlConnectionString, setting);

        if (npgsql.Password is not null)
        {
            throw Invalid(setting, $"Password cannot be combined with {AuthenticationKeyword}, which supplies the token as the password. Remove Password.");
        }

        if (npgsql.Passfile is not null)
        {
            throw Invalid(setting, $"Passfile cannot be combined with {AuthenticationKeyword}, which supplies the token as the password. Remove Passfile.");
        }

        if (string.IsNullOrWhiteSpace(npgsql.Username))
        {
            throw Invalid(setting, $"{AuthenticationKeyword} requires Username, the database role the token signs in as.");
        }

        var hosts = HostNames(npgsql.Host);

        return isAzure ? ForAzure(hosts, scopeOverride, setting) : ForAws(hosts, setting);
    }

    static bool IsAzureEntraId(string? authentication, string setting)
    {
        if (string.Equals(authentication, AzureEntraId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(authentication, AwsRdsIam, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw Invalid(setting, $"{AuthenticationKeyword} has an unknown value. Valid values: {AzureEntraId}, {AwsRdsIam}.");
    }

    static string ReadScope(string? scope, string setting) =>
        Uri.TryCreate(scope, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? scope!
            : throw Invalid(setting, $"{ScopeKeyword} must be an absolute https URI.");

    static AzureEntraIdTokenAuthentication ForAzure(string[] hosts, string? scopeOverride, string setting)
    {
        var clouds = hosts.Select(AzureEntraIdCloud.ForHost).ToArray();

        if (clouds.OfType<AzureEntraIdCloud>().Distinct().Skip(1).Any())
        {
            throw Invalid(setting, $"{AuthenticationKeyword}={AzureEntraId} lists hosts in different Azure clouds, and one token cannot sign in to all of them.");
        }

        if (scopeOverride is not null)
        {
            return new AzureEntraIdTokenAuthentication(scopeOverride, AzureEntraIdCloud.ForScope(scopeOverride)?.AuthorityHost);
        }

        if (clouds.Length == 0 || clouds.Any(cloud => cloud is null))
        {
            throw Invalid(setting, $"{AuthenticationKeyword}={AzureEntraId} cannot tell the Azure cloud from the host name. Add {ScopeKeyword} with the Entra ID scope of the server.");
        }

        return new AzureEntraIdTokenAuthentication(clouds[0]!.Scope, clouds[0]!.AuthorityHost);
    }

    static AwsRdsIamTokenAuthentication ForAws(string[] hosts, string setting) =>
        hosts.Length == 1
            ? new AwsRdsIamTokenAuthentication()
            : throw Invalid(setting, $"{AuthenticationKeyword}={AwsRdsIam} requires exactly one host, because an IAM token is signed for one host and port.");

    static string[] HostNames(string? host) =>
        (host ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(WithoutPort)
            .ToArray();

    static string WithoutPort(string host)
    {
        var portSeparator = host.LastIndexOf(':');

        return portSeparator > 0 && host.IndexOf(':') == portSeparator ? host[..portSeparator] : host;
    }

    static KeywordBuilder CreateBuilder(string connectionString, string setting)
    {
        try
        {
            return new KeywordBuilder { ConnectionString = connectionString };
        }
        catch (ArgumentException e)
        {
            throw NotAConnectionString(setting, e);
        }
    }

    static NpgsqlConnectionStringBuilder CreateNpgsqlBuilder(string connectionString, string setting)
    {
        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception e) when (e is ArgumentException or KeyNotFoundException)
        {
            throw NotAConnectionString(setting, e);
        }
    }

    static InvalidOperationException NotAConnectionString(string setting, Exception e) => new($"{setting} is not a valid connection string. {e.Message}");

    static InvalidOperationException Invalid(string setting, string reason) => new($"{setting} is invalid. {reason}");

    sealed class KeywordBuilder : DbConnectionStringBuilder
    {
        public HashSet<string> EmptyKeywords { get; } = new(StringComparer.OrdinalIgnoreCase);

        // Setting ConnectionString calls Remove, and stores nothing, for a keyword written with an empty value.
        public override bool Remove(string keyword)
        {
            EmptyKeywords.Add(keyword);
            return base.Remove(keyword);
        }
    }
}

sealed record AzureEntraIdTokenAuthentication(string Scope, Uri? AuthorityHost) : PostgreSqlTokenAuthentication
{
    public override IPostgreSqlTokenProvider CreateTokenProvider() => new AzureEntraIdTokenProvider(this);
}

sealed record AwsRdsIamTokenAuthentication : PostgreSqlTokenAuthentication
{
    public override IPostgreSqlTokenProvider CreateTokenProvider() => new AwsRdsIamTokenProvider();
}
