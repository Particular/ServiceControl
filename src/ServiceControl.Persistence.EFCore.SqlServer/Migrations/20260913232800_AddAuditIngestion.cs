using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditMessages",
                columns: table => new
                {
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UniqueMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeSent = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConversationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsSystemMessage = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SendingEndpointName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SendingEndpointHostId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SendingEndpointHost = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReceivingEndpointName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReceivingEndpointHostId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivingEndpointHost = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CriticalTimeTicks = table.Column<long>(type: "bigint", nullable: true),
                    ProcessingTimeTicks = table.Column<long>(type: "bigint", nullable: true),
                    DeliveryTimeTicks = table.Column<long>(type: "bigint", nullable: true),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyStoredExternally = table.Column<bool>(type: "bit", nullable: false),
                    BodySize = table.Column<int>(type: "int", nullable: false),
                    BodyContentType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditMessages", x => new { x.CreatedOn, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "FailedAuditImports",
                columns: table => new
                {
                    UniqueMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FailedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    BodyStoredExternally = table.Column<bool>(type: "bit", nullable: false),
                    ExceptionInfo = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedAuditImports", x => x.UniqueMessageId);
                });

            migrationBuilder.CreateTable(
                name: "SagaSnapshots",
                columns: table => new
                {
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SagaType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    StateAfterChange = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitiatingMessageJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutgoingMessagesJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaSnapshots", x => new { x.CreatedOn, x.Id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditMessages_ConversationId",
                table: "AuditMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditMessages_ProcessedAt",
                table: "AuditMessages",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditMessages_ReceivingEndpointName_CreatedOn",
                table: "AuditMessages",
                columns: new[] { "ReceivingEndpointName", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditMessages_TimeSent",
                table: "AuditMessages",
                column: "TimeSent");

            migrationBuilder.CreateIndex(
                name: "IX_AuditMessages_UniqueMessageId",
                table: "AuditMessages",
                column: "UniqueMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedAuditImports_FailedAt",
                table: "FailedAuditImports",
                column: "FailedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SagaSnapshots_SagaId_FinishTime",
                table: "SagaSnapshots",
                columns: new[] { "SagaId", "FinishTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditMessages");

            migrationBuilder.DropTable(
                name: "FailedAuditImports");

            migrationBuilder.DropTable(
                name: "SagaSnapshots");
        }
    }
}
