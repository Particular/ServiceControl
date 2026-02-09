using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace ServiceControl.Persistence.Sql.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryTsVectorColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "Query",
                table: "FailedMessages",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector('english', coalesce(headers_json::text, '')), 'A') || setweight(to_tsvector('english', coalesce(body, '')), 'B')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Query",
                table: "FailedMessages",
                column: "Query")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedMessages_Query",
                table: "FailedMessages");

            migrationBuilder.DropColumn(
                name: "Query",
                table: "FailedMessages");
        }
    }
}
