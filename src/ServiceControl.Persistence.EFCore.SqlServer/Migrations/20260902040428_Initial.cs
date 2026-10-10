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
                name: "ArchiveOperations",
                columns: table => new
                {
                    RequestId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArchiveType = table.Column<int>(type: "int", nullable: false),
                    OperationType = table.Column<int>(type: "int", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalNumberOfMessages = table.Column<int>(type: "int", nullable: false),
                    NumberOfMessagesProcessed = table.Column<int>(type: "int", nullable: false),
                    NumberOfBatches = table.Column<int>(type: "int", nullable: false),
                    CurrentBatch = table.Column<int>(type: "int", nullable: false),
                    Started = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InitiatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    InitiatedByName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    OperationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveOperations", x => new { x.RequestId, x.ArchiveType, x.OperationType });
                });

            migrationBuilder.CreateTable(
                name: "CustomChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomCheckId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginatingEndpointName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginatingEndpointHostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginatingEndpointHost = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomChecks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EndpointSettings",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TrackInstances = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndpointSettings", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "EventLogItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RelatedTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventLogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalIntegrationDispatchRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DispatchContextTypeName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DispatchContextJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIntegrationDispatchRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FailedErrorImports",
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
                    table.PrimaryKey("PK_FailedErrorImports", x => x.UniqueMessageId);
                });

            migrationBuilder.CreateTable(
                name: "FailedMessageEdits",
                columns: table => new
                {
                    UniqueMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessageEdits", x => x.UniqueMessageId);
                });

            migrationBuilder.CreateTable(
                name: "FailedMessageRetries",
                columns: table => new
                {
                    UniqueMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetryBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageAttempts = table.Column<int>(type: "int", nullable: false)
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
                    BodyContentType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FailingEndpointAddress = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessages", x => x.UniqueMessageId);
                });

            migrationBuilder.CreateTable(
                name: "GroupComments",
                columns: table => new
                {
                    GroupId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupComments", x => x.GroupId);
                });

            migrationBuilder.CreateTable(
                name: "HistoricRetryOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RetryType = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Originator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Failed = table.Column<bool>(type: "bit", nullable: false),
                    NumberOfMessagesProcessed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricRetryOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnownEndpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    HostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Monitored = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LicensingEndpoints",
                columns: table => new
                {
                    NormalizedName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ThroughputSource = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SanitizedName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    NormalizedSanitizedName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserIndicator = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EndpointIndicators = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicensingEndpoints", x => new { x.NormalizedName, x.ThroughputSource });
                });

            migrationBuilder.CreateTable(
                name: "MessageRedirects",
                columns: table => new
                {
                    FromPhysicalAddress = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ToPhysicalAddress = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageRedirects", x => x.FromPhysicalAddress);
                });

            migrationBuilder.CreateTable(
                name: "RetryBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RetrySessionId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RetryType = table.Column<int>(type: "int", nullable: false),
                    InitialBatchSize = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Last = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StagingId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Context = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Originator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Classifier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitiatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    InitiatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetryBatchNowForwarding",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    RetryBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryBatchNowForwarding", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    MessageType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TransportAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => new { x.MessageType, x.TransportAddress });
                });

            migrationBuilder.CreateTable(
                name: "UnacknowledgedRetryOperations",
                columns: table => new
                {
                    RequestId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RetryType = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Last = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Originator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Classifier = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Failed = table.Column<bool>(type: "bit", nullable: false),
                    NumberOfMessagesProcessed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnacknowledgedRetryOperations", x => new { x.RequestId, x.RetryType });
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

            migrationBuilder.CreateTable(
                name: "LicensingEndpointThroughput",
                columns: table => new
                {
                    NormalizedName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ThroughputSource = table.Column<int>(type: "int", nullable: false),
                    DateUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    MessageCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicensingEndpointThroughput", x => new { x.NormalizedName, x.ThroughputSource, x.DateUtc });
                    table.ForeignKey(
                        name: "FK_LicensingEndpointThroughput_LicensingEndpoints_NormalizedName_ThroughputSource",
                        columns: x => new { x.NormalizedName, x.ThroughputSource },
                        principalTable: "LicensingEndpoints",
                        principalColumns: new[] { "NormalizedName", "ThroughputSource" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveOperations_Started",
                table: "ArchiveOperations",
                column: "Started");

            migrationBuilder.CreateIndex(
                name: "IX_CustomChecks_ReportedAt",
                table: "CustomChecks",
                column: "ReportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomChecks_Status_ReportedAt",
                table: "CustomChecks",
                columns: new[] { "Status", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EventLogItems_RaisedAt_Id",
                table: "EventLogItems",
                columns: new[] { "RaisedAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_FailedErrorImports_FailedAt",
                table: "FailedErrorImports",
                column: "FailedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageEdits_EditId",
                table: "FailedMessageEdits",
                column: "EditId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageGroups_GroupId",
                table: "FailedMessageGroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageGroups_Type_GroupId",
                table: "FailedMessageGroups",
                columns: new[] { "Type", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageRetries_RetryBatchId",
                table: "FailedMessageRetries",
                column: "RetryBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_ConversationId",
                table: "FailedMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_FailingEndpointAddress",
                table: "FailedMessages",
                column: "FailingEndpointAddress");

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

            migrationBuilder.CreateIndex(
                name: "IX_HistoricRetryOperations_CompletionTime_Id",
                table: "HistoricRetryOperations",
                columns: new[] { "CompletionTime", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_LicensingEndpoints_NormalizedSanitizedName",
                table: "LicensingEndpoints",
                column: "NormalizedSanitizedName");

            migrationBuilder.CreateIndex(
                name: "IX_LicensingEndpointThroughput_DateUtc",
                table: "LicensingEndpointThroughput",
                column: "DateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RetryBatches_Status_RetrySessionId",
                table: "RetryBatches",
                columns: new[] { "Status", "RetrySessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveOperations");

            migrationBuilder.DropTable(
                name: "CustomChecks");

            migrationBuilder.DropTable(
                name: "EndpointSettings");

            migrationBuilder.DropTable(
                name: "EventLogItems");

            migrationBuilder.DropTable(
                name: "ExternalIntegrationDispatchRequests");

            migrationBuilder.DropTable(
                name: "FailedErrorImports");

            migrationBuilder.DropTable(
                name: "FailedMessageEdits");

            migrationBuilder.DropTable(
                name: "FailedMessageGroups");

            migrationBuilder.DropTable(
                name: "FailedMessageRetries");

            migrationBuilder.DropTable(
                name: "GroupComments");

            migrationBuilder.DropTable(
                name: "HistoricRetryOperations");

            migrationBuilder.DropTable(
                name: "KnownEndpoints");

            migrationBuilder.DropTable(
                name: "LicensingEndpointThroughput");

            migrationBuilder.DropTable(
                name: "MessageRedirects");

            migrationBuilder.DropTable(
                name: "RetryBatches");

            migrationBuilder.DropTable(
                name: "RetryBatchNowForwarding");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "UnacknowledgedRetryOperations");

            migrationBuilder.DropTable(
                name: "FailedMessages");

            migrationBuilder.DropTable(
                name: "LicensingEndpoints");
        }
    }
}
