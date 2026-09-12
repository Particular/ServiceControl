using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingIndexes : Migration
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

            migrationBuilder.AlterColumn<string>(
                name: "message_type",
                table: "failed_messages",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_status_last_modified",
                table: "failed_messages",
                columns: new[] { "status", "last_modified" })
                .Annotation("Npgsql:IndexInclude", new[] { "first_time_of_failure", "last_time_of_failure" });

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_status_last_time_of_failure",
                table: "failed_messages",
                columns: new[] { "status", "last_time_of_failure" });

            // Normally this would be potentially slow and necessitate executing outside of the
            // migration transaction, but no customers consume this persister yet so this can
            // be treated as an initial migration
            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_status_message_type_unique_message_id",
                table: "failed_messages",
                columns: new[] { "status", "message_type", "unique_message_id" });

            migrationBuilder.CreateIndex(
                name: "ix_failed_message_groups_type_group_id",
                table: "failed_message_groups",
                columns: new[] { "type", "group_id" })
                .Annotation("Npgsql:IndexInclude", new[] { "title" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_failed_messages_status_last_modified",
                table: "failed_messages");

            migrationBuilder.DropIndex(
                name: "ix_failed_messages_status_last_time_of_failure",
                table: "failed_messages");

            migrationBuilder.DropIndex(
                name: "ix_failed_messages_status_message_type_unique_message_id",
                table: "failed_messages");

            migrationBuilder.DropIndex(
                name: "ix_failed_message_groups_type_group_id",
                table: "failed_message_groups");

            migrationBuilder.AlterColumn<string>(
                name: "message_type",
                table: "failed_messages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_status_last_modified",
                table: "failed_messages",
                columns: new[] { "status", "last_modified" });

            migrationBuilder.CreateIndex(
                name: "ix_failed_message_groups_type_group_id",
                table: "failed_message_groups",
                columns: new[] { "type", "group_id" });
        }
    }
}
