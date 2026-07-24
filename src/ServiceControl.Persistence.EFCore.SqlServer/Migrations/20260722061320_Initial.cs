using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FailedMessageRetries",
                columns: table => new
                {
                    UniqueMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetryId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessageRetries", x => x.UniqueMessageId);
                });

            migrationBuilder.CreateTable(
                name: "FailedMessages",
                columns: table => new
                {
                    UniqueMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NumberOfProcessingAttempts = table.Column<int>(type: "int", nullable: false),
                    FirstTimeOfFailure = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastTimeOfFailure = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeSent = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConversationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    QueueAddress = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SendingEndpointName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SendingEndpointHostId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SendingEndpointHost = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReceivingEndpointName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReceivingEndpointHostId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivingEndpointHost = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ExceptionType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExceptionMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSystemMessage = table.Column<bool>(type: "bit", nullable: false),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyStoredExternally = table.Column<bool>(type: "bit", nullable: false),
                    BodySize = table.Column<int>(type: "int", nullable: false),
                    BodyContentType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessages", x => x.UniqueMessageId);
                });

            migrationBuilder.CreateTable(
                name: "KnownEndpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Monitored = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FailedMessageGroups",
                columns: table => new
                {
                    FailedMessageUniqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessageGroups", x => new { x.FailedMessageUniqueId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_FailedMessageGroups_FailedMessages_FailedMessageUniqueId",
                        column: x => x.FailedMessageUniqueId,
                        principalTable: "FailedMessages",
                        principalColumn: "UniqueMessageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageGroups_GroupId",
                table: "FailedMessageGroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_ConversationId",
                table: "FailedMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_QueueAddress",
                table: "FailedMessages",
                column: "QueueAddress");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_ReceivingEndpointName",
                table: "FailedMessages",
                column: "ReceivingEndpointName");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Status_LastModified",
                table: "FailedMessages",
                columns: new[] { "Status", "LastModified" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_StatusChangedAt",
                table: "FailedMessages",
                column: "StatusChangedAt",
                filter: "[Status] IN (2, 4)");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_TimeSent",
                table: "FailedMessages",
                column: "TimeSent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailedMessageGroups");

            migrationBuilder.DropTable(
                name: "FailedMessageRetries");

            migrationBuilder.DropTable(
                name: "KnownEndpoints");

            migrationBuilder.DropTable(
                name: "FailedMessages");
        }
    }
}
