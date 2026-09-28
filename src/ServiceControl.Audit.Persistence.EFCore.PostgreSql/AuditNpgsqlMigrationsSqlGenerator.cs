namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

#pragma warning disable EF1001 // Internal EF Core API usage
sealed class AuditNpgsqlMigrationsSqlGenerator(
    MigrationsSqlGeneratorDependencies dependencies,
    INpgsqlSingletonOptions npgsqlSingletonOptions,
    IDbContextOptions contextOptions)
    : NpgsqlMigrationsSqlGenerator(dependencies, npgsqlSingletonOptions)
{
    public static readonly string[] PartitionedTables = ["audit_messages", "saga_snapshots"];

    const string PartitionKey = "created_on";

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

    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!PartitionedTables.Contains(operation.Name))
        {
            base.Generate(operation, model, builder, terminate);
            return;
        }

        base.Generate(operation, model, builder, terminate: false);
        builder.Append($" PARTITION BY RANGE ({Dependencies.SqlGenerationHelper.DelimitIdentifier(PartitionKey)})");

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }
    }
}
#pragma warning restore EF1001
