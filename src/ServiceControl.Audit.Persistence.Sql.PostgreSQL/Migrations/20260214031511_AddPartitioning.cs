using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddPartitioning : Migration
    {
        static readonly string[] Tables = ["processed_messages", "saga_snapshots"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"""
                    CREATE TABLE {table}_tmp (LIKE {table} INCLUDING ALL);
                    DROP TABLE {table};
                    CREATE TABLE {table} (LIKE {table}_tmp INCLUDING ALL) PARTITION BY RANGE (created_on);
                    DROP TABLE {table}_tmp;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"""
                    CREATE TABLE {table}_tmp (LIKE {table} INCLUDING ALL);
                    DROP TABLE {table};
                    CREATE TABLE {table} (LIKE {table}_tmp INCLUDING ALL);
                    DROP TABLE {table}_tmp;
                    """);
            }
        }
    }
}
