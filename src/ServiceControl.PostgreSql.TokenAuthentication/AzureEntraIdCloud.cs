#nullable enable
namespace ServiceControl.PostgreSql.TokenAuthentication;

using System;
using System.Linq;
using Azure.Identity;

/// <summary>
/// The scopes are <c>ossrdbms_resource_id</c> in the Azure CLI's cloud definitions. A null authority host leaves <see cref="DefaultAzureCredentialOptions.AuthorityHost"/> at its default, which honors AZURE_AUTHORITY_HOST.
/// </summary>
sealed record AzureEntraIdCloud(string HostSuffix, string Scope, Uri? AuthorityHost)
{
    public static readonly AzureEntraIdCloud Public = new(".postgres.database.azure.com", "https://ossrdbms-aad.database.windows.net/.default", null);
    public static readonly AzureEntraIdCloud Government = new(".postgres.database.usgovcloudapi.net", "https://ossrdbms-aad.database.usgovcloudapi.net/.default", AzureAuthorityHosts.AzureGovernment);
    public static readonly AzureEntraIdCloud China = new(".postgres.database.chinacloudapi.cn", "https://ossrdbms-aad.database.chinacloudapi.cn/.default", AzureAuthorityHosts.AzureChina);

    static readonly AzureEntraIdCloud[] all = [Public, Government, China];

    public static AzureEntraIdCloud? ForHost(string host) =>
        all.FirstOrDefault(cloud => host.EndsWith(cloud.HostSuffix, StringComparison.OrdinalIgnoreCase));

    public static AzureEntraIdCloud? ForScope(string scope) =>
        all.FirstOrDefault(cloud => string.Equals(cloud.Scope, scope, StringComparison.OrdinalIgnoreCase));
}
