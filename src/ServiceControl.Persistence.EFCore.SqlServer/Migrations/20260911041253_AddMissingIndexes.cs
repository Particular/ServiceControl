using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedMessages_Status_LastModified",
                table: "FailedMessages");

            migrationBuilder.DropIndex(
                name: "IX_FailedMessageGroups_Type_GroupId",
                table: "FailedMessageGroups");

            migrationBuilder.AlterColumn<string>(
                name: "MessageType",
                table: "FailedMessages",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Status_LastModified",
                table: "FailedMessages",
                columns: new[] { "Status", "LastModified" })
                .Annotation("SqlServer:Include", new[] { "FirstTimeOfFailure", "LastTimeOfFailure" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Status_LastTimeOfFailure",
                table: "FailedMessages",
                columns: new[] { "Status", "LastTimeOfFailure" });

            // Normally this would be potentially slow and necessitate executing outside of the
            // migration transaction, but no customers consume this persister yet so this can
            // be treated as an initial migration
            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Status_MessageType_UniqueMessageId",
                table: "FailedMessages",
                columns: new[] { "Status", "MessageType", "UniqueMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageGroups_Type_GroupId",
                table: "FailedMessageGroups",
                columns: new[] { "Type", "GroupId" })
                .Annotation("SqlServer:Include", new[] { "Title" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedMessages_Status_LastModified",
                table: "FailedMessages");

            migrationBuilder.DropIndex(
                name: "IX_FailedMessages_Status_LastTimeOfFailure",
                table: "FailedMessages");

            migrationBuilder.DropIndex(
                name: "IX_FailedMessages_Status_MessageType_UniqueMessageId",
                table: "FailedMessages");

            migrationBuilder.DropIndex(
                name: "IX_FailedMessageGroups_Type_GroupId",
                table: "FailedMessageGroups");

            migrationBuilder.AlterColumn<string>(
                name: "MessageType",
                table: "FailedMessages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Status_LastModified",
                table: "FailedMessages",
                columns: new[] { "Status", "LastModified" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageGroups_Type_GroupId",
                table: "FailedMessageGroups",
                columns: new[] { "Type", "GroupId" });
        }
    }
}
