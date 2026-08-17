using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedMessageEdits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "failed_message_edits",
                columns: table => new
                {
                    unique_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edit_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_failed_message_edits", x => x.unique_message_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_failed_message_edits_edit_id",
                table: "failed_message_edits",
                column: "edit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "failed_message_edits");
        }
    }
}
