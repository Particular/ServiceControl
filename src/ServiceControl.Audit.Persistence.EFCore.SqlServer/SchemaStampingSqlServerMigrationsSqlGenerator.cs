namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Update;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

sealed class SchemaStampingSqlServerMigrationsSqlGenerator(
    MigrationsSqlGeneratorDependencies dependencies,
    ICommandBatchPreparer commandBatchPreparer,
    IDbContextOptions contextOptions)
    : SqlServerMigrationsSqlGenerator(dependencies, commandBatchPreparer)
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
