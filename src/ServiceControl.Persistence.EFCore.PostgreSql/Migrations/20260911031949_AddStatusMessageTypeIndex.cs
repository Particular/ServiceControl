using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusMessageTypeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "message_type",
                table: "failed_messages",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            //normally this would be potentially slow and necessitate executing outside of the
            //migration transaction, but no customers consume this persister yet so this can
            //treated as an initial migration
            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_status_message_type_unique_message_id",
                table: "failed_messages",
                columns: new[] { "status", "message_type", "unique_message_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_failed_messages_status_message_type_unique_message_id",
                table: "failed_messages");

            migrationBuilder.AlterColumn<string>(
                name: "message_type",
                table: "failed_messages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
