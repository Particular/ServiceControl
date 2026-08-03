using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveOperations",
                columns: table => new
                {
                    RequestId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArchiveType = table.Column<int>(type: "int", nullable: false),
                    IsArchive = table.Column<bool>(type: "bit", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalNumberOfMessages = table.Column<int>(type: "int", nullable: false),
                    NumberOfMessagesProcessed = table.Column<int>(type: "int", nullable: false),
                    NumberOfBatches = table.Column<int>(type: "int", nullable: false),
                    CurrentBatch = table.Column<int>(type: "int", nullable: false),
                    Started = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InitiatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    InitiatedByName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    OperationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveOperations", x => new { x.RequestId, x.ArchiveType, x.IsArchive });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveOperations_Started",
                table: "ArchiveOperations",
                column: "Started");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveOperations");
        }
    }
}
