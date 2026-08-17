using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "group_comments",
                columns: table => new
                {
                    group_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    comment = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_comments", x => x.group_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_comments");
        }
    }
}
