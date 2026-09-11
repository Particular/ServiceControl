using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusMessageTypeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MessageType",
                table: "FailedMessages",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            //normally this would be potentially slow and necessitate executing outside of the
            //migration transaction, but no customers consume this persister yet so this can
            //treated as an initial migration
            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Status_MessageType_UniqueMessageId",
                table: "FailedMessages",
                columns: new[] { "Status", "MessageType", "UniqueMessageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedMessages_Status_MessageType_UniqueMessageId",
                table: "FailedMessages");

            migrationBuilder.AlterColumn<string>(
                name: "MessageType",
                table: "FailedMessages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
