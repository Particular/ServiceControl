namespace ServiceControl.Persistence.Sql.PostgreSQL;

using System.Text;
using Core.DbContexts;
using Microsoft.EntityFrameworkCore;

class PostgreSqlDbContext : ServiceControlDbContextBase
{
    public PostgreSqlDbContext(DbContextOptions<PostgreSqlDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply snake_case naming convention for PostgreSQL
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName != null)
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entity.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (columnName != null)
                {
                    property.SetColumnName(ToSnakeCase(columnName));
                }
            }

            foreach (var key in entity.GetKeys())
            {
                var keyName = key.GetName();
                if (keyName != null)
                {
                    key.SetName(ToSnakeCase(keyName));
                }
            }

            foreach (var index in entity.GetIndexes())
            {
                var indexName = index.GetDatabaseName();
                if (indexName != null)
                {
                    index.SetDatabaseName(ToSnakeCase(indexName));
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && name[i - 1] != '_')
                {
                    builder.Append('_');
                }
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    protected override void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
        // PostgreSQL full-text search optimization
        // Add a computed tsvector column combining headers (weight A) and body (weight B)
        var failedMessages = modelBuilder.Entity<Core.Entities.FailedMessageEntity>();

        failedMessages
            .Property<NpgsqlTypes.NpgsqlTsVector>("Query")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "setweight(to_tsvector('english', coalesce(headers_json::text, '')), 'A') || " +
                "setweight(to_tsvector('english', coalesce(body, '')), 'B')",
                stored: true);

        // Add GIN index on tsvector for fast full-text search
        failedMessages
            .HasIndex("Query")
            .HasMethod("GIN");
    }
}
