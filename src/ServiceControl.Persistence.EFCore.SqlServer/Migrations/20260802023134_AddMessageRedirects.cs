using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageRedirects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageRedirects",
                columns: table => new
                {
                    FromPhysicalAddress = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ToPhysicalAddress = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageRedirects", x => x.FromPhysicalAddress);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageRedirects");
        }
    }
}
