using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class UseSimpleFtsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the existing index first
            migrationBuilder.DropIndex(
                name: "ix_processed_messages_query",
                table: "processed_messages");

            // Drop and recreate the computed column with simple configuration (no stemming/stop words)
            migrationBuilder.DropColumn(
                name: "query",
                table: "processed_messages");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "query",
                table: "processed_messages",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('simple', coalesce(headers_json, '') || ' ' || coalesce(body, ''))",
                stored: true);

            // Recreate the GIN index
            migrationBuilder.CreateIndex(
                name: "ix_processed_messages_query",
                table: "processed_messages",
                column: "query")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the simple index
            migrationBuilder.DropIndex(
                name: "ix_processed_messages_query",
                table: "processed_messages");

            // Restore the weighted tsvector column
            migrationBuilder.DropColumn(
                name: "query",
                table: "processed_messages");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "query",
                table: "processed_messages",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector('english', coalesce(headers_json::text, '')), 'A') || setweight(to_tsvector('english', coalesce(body, '')), 'B')",
                stored: true);

            // Recreate the GIN index
            migrationBuilder.CreateIndex(
                name: "ix_processed_messages_query",
                table: "processed_messages",
                column: "query")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }
    }
}
