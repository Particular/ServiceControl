using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class WidenGroupAggregateIndexes : Migration
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

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Status_LastModified",
                table: "FailedMessages",
                columns: new[] { "Status", "LastModified" })
                .Annotation("SqlServer:Include", new[] { "FirstTimeOfFailure", "LastTimeOfFailure" });

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
                name: "IX_FailedMessageGroups_Type_GroupId",
                table: "FailedMessageGroups");

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
