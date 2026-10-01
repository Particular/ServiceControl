namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

static class BatchInsert
{
    public static async Task Rows<TEntity>(DbContext dbContext, IReadOnlyCollection<TEntity> rows, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        if (rows.Count == 0)
        {
            return;
        }

        var table = InsertTable.For<TEntity>(dbContext);
        var transaction = (dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Batch inserts must run inside a transaction")).GetDbTransaction();

        foreach (var chunk in rows.Chunk(table.RowsPerStatement))
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = dbContext.Database.GetCommandTimeout() ?? command.CommandTimeout;
            command.CommandText = table.Sql(chunk.Length);

            var index = 0;
            foreach (var row in chunk)
            {
                foreach (var column in table.Columns)
                {
                    command.Parameters.Add(column.TypeMapping.CreateParameter(command, $"@p{index++}", column.Read(row), column.IsNullable));
                }
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    sealed class InsertTable
    {
        InsertTable(string qualifiedName, IReadOnlyList<InsertColumn> columns, string columnList)
        {
            this.qualifiedName = qualifiedName;
            this.columnList = columnList;
            Columns = columns;
            RowsPerStatement = Math.Max(1, Math.Min(50, MaxParametersPerStatement / columns.Count));
        }

        public IReadOnlyList<InsertColumn> Columns { get; }

        public int RowsPerStatement { get; }

        public string Sql(int rowCount)
        {
            var sql = new StringBuilder($"INSERT INTO {qualifiedName} ({columnList}) VALUES ");

            for (var row = 0; row < rowCount; row++)
            {
                sql.Append(row == 0 ? "(" : ", (");

                for (var column = 0; column < Columns.Count; column++)
                {
                    if (column > 0)
                    {
                        sql.Append(", ");
                    }

                    sql.Append("@p").Append((row * Columns.Count) + column);
                }

                sql.Append(')');
            }

            return sql.ToString();
        }

        public static InsertTable For<TEntity>(DbContext dbContext) =>
            cache.GetOrAdd((dbContext.Model, typeof(TEntity)), static (key, context) =>
            {
                var entityType = key.Model.FindEntityType(key.EntityType)
                    ?? throw new InvalidOperationException($"{key.EntityType.Name} is not part of the model.");
                var tableName = entityType.GetTableName()
                    ?? throw new InvalidOperationException($"{key.EntityType.Name} is not mapped to a table.");
                var schema = entityType.GetSchema();
                var storeObject = StoreObjectIdentifier.Table(tableName, schema);
                var sqlGenerationHelper = context.GetService<ISqlGenerationHelper>();

                InsertColumn[] columns =
                [
                    .. entityType.GetProperties()
                        .Where(property => property.ValueGenerated != ValueGenerated.OnAdd)
                        .Select(property => new InsertColumn(
                            property.GetColumnName(storeObject)!,
                            property.GetRelationalTypeMapping(),
                            property.IsNullable,
                            CompileGetter(property.PropertyInfo!)))
                ];

                return new InsertTable(
                    sqlGenerationHelper.DelimitIdentifier(tableName, schema),
                    columns,
                    string.Join(", ", columns.Select(column => sqlGenerationHelper.DelimitIdentifier(column.Name))));
            }, dbContext);

        static Func<object, object?> CompileGetter(PropertyInfo property)
        {
            var entity = Expression.Parameter(typeof(object));
            var value = Expression.Property(Expression.Convert(entity, property.DeclaringType!), property);
            return Expression.Lambda<Func<object, object?>>(Expression.Convert(value, typeof(object)), entity).Compile();
        }

        readonly string qualifiedName;
        readonly string columnList;

        const int MaxParametersPerStatement = 2000;

        static readonly ConcurrentDictionary<(IModel Model, Type EntityType), InsertTable> cache = new();
    }

    sealed record InsertColumn(string Name, RelationalTypeMapping TypeMapping, bool IsNullable, Func<object, object?> Read);
}
