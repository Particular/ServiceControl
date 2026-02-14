using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FailedAuditImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExceptionInfo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedAuditImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnownEndpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnownEndpointsInsertOnly",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KnownEndpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownEndpointsInsertOnly", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UniqueMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SearchableContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MessageType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TimeSent = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsSystemMessage = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ConversationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceivingEndpointName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriticalTimeTicks = table.Column<long>(type: "bigint", nullable: true),
                    ProcessingTimeTicks = table.Column<long>(type: "bigint", nullable: true),
                    DeliveryTimeTicks = table.Column<long>(type: "bigint", nullable: true),
                    BodySize = table.Column<int>(type: "int", nullable: false),
                    BodyUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BodyNotStored = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMessages", x => new { x.Id, x.CreatedOn });
                });

            migrationBuilder.CreateTable(
                name: "SagaSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SagaType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StateAfterChange = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InitiatingMessageJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutgoingMessagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaSnapshots", x => new { x.Id, x.CreatedOn });
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnownEndpoints_LastSeen",
                table: "KnownEndpoints",
                column: "LastSeen");

            migrationBuilder.CreateIndex(
                name: "IX_KnownEndpointsInsertOnly_KnownEndpointId",
                table: "KnownEndpointsInsertOnly",
                column: "KnownEndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_KnownEndpointsInsertOnly_LastSeen",
                table: "KnownEndpointsInsertOnly",
                column: "LastSeen");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_ConversationId",
                table: "ProcessedMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_MessageId",
                table: "ProcessedMessages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_TimeSent",
                table: "ProcessedMessages",
                column: "TimeSent");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_UniqueMessageId",
                table: "ProcessedMessages",
                column: "UniqueMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_SagaSnapshots_SagaId",
                table: "SagaSnapshots",
                column: "SagaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailedAuditImports");

            migrationBuilder.DropTable(
                name: "KnownEndpoints");

            migrationBuilder.DropTable(
                name: "KnownEndpointsInsertOnly");

            migrationBuilder.DropTable(
                name: "ProcessedMessages");

            migrationBuilder.DropTable(
                name: "SagaSnapshots");
        }
    }
}
