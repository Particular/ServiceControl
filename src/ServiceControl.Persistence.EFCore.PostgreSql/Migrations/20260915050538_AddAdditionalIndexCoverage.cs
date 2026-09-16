using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddAdditionalIndexCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_failed_messages_status_last_modified",
                table: "failed_messages");

            migrationBuilder.DropIndex(
                name: "ix_failed_message_groups_type_group_id",
                table: "failed_message_groups");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_status_last_modified_unique_message_id",
                table: "failed_messages",
                columns: new[] { "status", "last_modified", "unique_message_id" })
                .Annotation("Npgsql:IndexInclude", new[] { "first_time_of_failure", "last_time_of_failure" });

            migrationBuilder.CreateIndex(
                name: "ix_failed_message_groups_type_group_id",
                table: "failed_message_groups",
                columns: new[] { "type", "group_id" })
                .Annotation("Npgsql:IndexInclude", new[] { "failed_message_unique_id", "title" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_failed_messages_status_last_modified_unique_message_id",
                table: "failed_messages");

            migrationBuilder.DropIndex(
                name: "ix_failed_message_groups_type_group_id",
                table: "failed_message_groups");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_status_last_modified",
                table: "failed_messages",
                columns: new[] { "status", "last_modified" })
                .Annotation("Npgsql:IndexInclude", new[] { "first_time_of_failure", "last_time_of_failure" });

            migrationBuilder.CreateIndex(
                name: "ix_failed_message_groups_type_group_id",
                table: "failed_message_groups",
                columns: new[] { "type", "group_id" })
                .Annotation("Npgsql:IndexInclude", new[] { "title" });
        }
    }
}
