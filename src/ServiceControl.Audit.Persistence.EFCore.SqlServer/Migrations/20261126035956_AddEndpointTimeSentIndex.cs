using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddEndpointTimeSentIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AuditMessages_ReceivingEndpointName_TimeSent",
                table: "AuditMessages",
                columns: new[] { "ReceivingEndpointName", "TimeSent" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditMessages_ReceivingEndpointName_TimeSent",
                table: "AuditMessages");
        }
    }
}
