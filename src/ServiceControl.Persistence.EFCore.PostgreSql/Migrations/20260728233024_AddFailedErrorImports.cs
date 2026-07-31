using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedErrorImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "failed_error_imports",
                columns: table => new
                {
                    unique_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    message_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    headers_json = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<byte[]>(type: "bytea", nullable: false),
                    body_stored_externally = table.Column<bool>(type: "boolean", nullable: false),
                    exception_info = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_failed_error_imports", x => x.unique_message_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_failed_error_imports_failed_at",
                table: "failed_error_imports",
                column: "failed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "failed_error_imports");
        }
    }
}
