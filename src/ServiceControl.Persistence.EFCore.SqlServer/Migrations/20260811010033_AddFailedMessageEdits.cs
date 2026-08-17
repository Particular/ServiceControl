using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedMessageEdits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FailedMessageEdits",
                columns: table => new
                {
                    UniqueMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessageEdits", x => x.UniqueMessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageEdits_EditId",
                table: "FailedMessageEdits",
                column: "EditId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailedMessageEdits");
        }
    }
}
