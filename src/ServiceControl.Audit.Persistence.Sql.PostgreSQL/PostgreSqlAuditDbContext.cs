namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL;

using Core.DbContexts;
using Core.Entities;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

public class PostgreSqlAuditDbContext : AuditDbContextBase
{
    public PostgreSqlAuditDbContext(DbContextOptions<PostgreSqlAuditDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Call base first to apply entity configurations
        base.OnModelCreating(modelBuilder);

        // Apply snake_case naming convention for PostgreSQL
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Convert table names to snake_case
            var tableName = entity.GetTableName();
            if (tableName != null)
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            // Convert column names to snake_case
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            // Skip index name conversion - EF generates unique names and snake_case
            // conversion can cause collisions due to truncation

            // Convert foreign key names to snake_case
            foreach (var key in entity.GetForeignKeys())
            {
                var constraintName = key.GetConstraintName();
                if (constraintName != null)
                {
                    key.SetConstraintName(ToSnakeCase(constraintName));
                }
            }
        }
    }

    protected override void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessedMessageEntity>(entity =>
        {
            entity.Property<NpgsqlTsVector>("Query")
                .HasColumnName("query")
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    "to_tsvector('simple', coalesce(headers_json, '') || ' ' || coalesce(body, ''))",
                    stored: true);

            entity.HasIndex("Query")
                .HasDatabaseName("ix_processed_messages_query")
                .HasMethod("GIN");
        });
    }

    static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    result.Append('_');
                }
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}
