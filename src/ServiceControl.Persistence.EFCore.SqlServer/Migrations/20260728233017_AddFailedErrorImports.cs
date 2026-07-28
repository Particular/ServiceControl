using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedErrorImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FailedErrorImports",
                columns: table => new
                {
                    UniqueMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FailedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    BodyStoredExternally = table.Column<bool>(type: "bit", nullable: false),
                    ExceptionInfo = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedErrorImports", x => x.UniqueMessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FailedErrorImports_FailedAt",
                table: "FailedErrorImports",
                column: "FailedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailedErrorImports");
        }
    }
}
