namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;
using ServiceControl.Persistence.EFCore.Infrastructure;

/// <summary>
/// Points the scaffolded migrations at the configured schema as they are turned into SQL. Without
/// this they name no schema and every table would land wherever the search path happens to point.
/// </summary>
// Npgsql's own generator takes INpgsqlSingletonOptions, which it ships as an internal API, so
// deriving from it cannot be done without naming that type. The alternative is reimplementing
// PostgreSQL DDL generation, which is far worse.
#pragma warning disable EF1001 // Internal EF Core API usage
sealed class SchemaStampingNpgsqlMigrationsSqlGenerator(
    MigrationsSqlGeneratorDependencies dependencies,
    INpgsqlSingletonOptions npgsqlSingletonOptions,
    IDbContextOptions contextOptions)
    : NpgsqlMigrationsSqlGenerator(dependencies, npgsqlSingletonOptions)
{
    readonly string? schema = contextOptions.FindExtension<SchemaOptionsExtension>()?.Schema;

    public override IReadOnlyList<MigrationCommand> Generate(
        IReadOnlyList<MigrationOperation> operations,
        IModel? model = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        if (schema is null)
        {
            return base.Generate(operations, model, options);
        }

        MigrationOperation[] stamped =
        [
            .. operations.Select(operation => operation is SqlOperation sql
                ? FullTextSearchSql.Rewrite(sql, schema)
                : MigrationSchemaStamper.Stamp(operation, schema))
        ];

        return base.Generate(stamped, model, options);
    }
}
#pragma warning restore EF1001
