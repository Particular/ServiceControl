namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore.Migrations.Operations;

/// <summary>
/// Scaffolded migrations carry no schema, so every table they name resolves to the connection's
/// default schema. Stamping the configured schema onto the operations just before they become SQL
/// is what lets one set of migrations build the schema an installation was configured with,
/// without regenerating them or making the schema a scaffold-time decision.
/// </summary>
public static class MigrationSchemaStamper
{
    /// <summary>
    /// Sets the schema on the operation and on anything nested inside it, leaving a schema the
    /// migration set for itself alone. Throws for an operation type that has not been considered
    /// rather than letting it run against the wrong schema.
    /// </summary>
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

            // The schema is the operation's own subject, not something it sits inside. The history
            // repository emits one of these to create the schema it keeps its table in.
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
