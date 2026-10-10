namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore.Migrations.Operations;

public static class MigrationSchemaStamper
{
    public static MigrationOperation Stamp(MigrationOperation operation, string schema)
    {
        switch (operation)
        {
            case CreateTableOperation createTable:
                createTable.Schema ??= schema;
                foreach (var column in createTable.Columns)
                {
                    column.Schema ??= schema;
                }
                createTable.PrimaryKey?.Schema ??= schema;
                foreach (var foreignKey in createTable.ForeignKeys)
                {
                    foreignKey.Schema ??= schema;
                    foreignKey.PrincipalSchema ??= schema;
                }
                foreach (var uniqueConstraint in createTable.UniqueConstraints)
                {
                    uniqueConstraint.Schema ??= schema;
                }
                foreach (var checkConstraint in createTable.CheckConstraints)
                {
                    checkConstraint.Schema ??= schema;
                }
                break;

            case DropTableOperation dropTable:
                dropTable.Schema ??= schema;
                break;

            case CreateIndexOperation createIndex:
                createIndex.Schema ??= schema;
                break;

            case DropIndexOperation dropIndex:
                dropIndex.Schema ??= schema;
                break;

            case AddColumnOperation addColumn:
                addColumn.Schema ??= schema;
                break;

            case AlterColumnOperation alterColumn:
                alterColumn.Schema ??= schema;
                alterColumn.OldColumn.Schema ??= schema;
                break;

            case DropColumnOperation dropColumn:
                dropColumn.Schema ??= schema;
                break;

            case InsertDataOperation insertData:
                insertData.Schema ??= schema;
                break;

            case EnsureSchemaOperation:
            case DropSchemaOperation:
                break;

            default:
                throw new InvalidOperationException(
                    $"Migration operation {operation.GetType().Name} is not handled by {nameof(MigrationSchemaStamper)}, so it would run against the connection's default schema instead of '{schema}'. Add a case for it.");
        }

        return operation;
    }
}
