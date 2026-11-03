#nullable enable
namespace ServiceControl.PostgreSql.TokenAuthentication;

using System.Threading;
using System.Threading.Tasks;
using Npgsql;

interface IPostgreSqlTokenProvider
{
    ValueTask<string> GetToken(NpgsqlConnectionStringBuilder settings, CancellationToken cancellationToken = default);
}
