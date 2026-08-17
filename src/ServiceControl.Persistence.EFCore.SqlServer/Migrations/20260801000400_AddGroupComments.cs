using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroupComments",
                columns: table => new
                {
                    GroupId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupComments", x => x.GroupId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupComments");
        }
    }
}
