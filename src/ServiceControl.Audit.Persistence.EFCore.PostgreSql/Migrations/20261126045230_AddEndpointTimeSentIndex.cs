using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddEndpointTimeSentIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_audit_messages_receiving_endpoint_name_time_sent",
                table: "audit_messages",
                columns: new[] { "receiving_endpoint_name", "time_sent" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_messages_receiving_endpoint_name_time_sent",
                table: "audit_messages");
        }
    }
}
