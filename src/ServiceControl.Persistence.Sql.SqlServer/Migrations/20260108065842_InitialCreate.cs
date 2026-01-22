using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ArchiveType = table.Column<int>(type: "int", nullable: false),
                    ArchiveState = table.Column<int>(type: "int", nullable: false),
                    TotalNumberOfMessages = table.Column<int>(type: "int", nullable: false),
                    NumberOfMessagesArchived = table.Column<int>(type: "int", nullable: false),
                    NumberOfBatches = table.Column<int>(type: "int", nullable: false),
                    CurrentBatch = table.Column<int>(type: "int", nullable: false),
                    Started = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Last = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomCheckId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EndpointName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomChecks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyThroughput",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EndpointName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ThroughputSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MessageCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyThroughput", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EndpointSettings",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RelatedToJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
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
                    DispatchContextJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIntegrationDispatchRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FailedErrorImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExceptionInfo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedErrorImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FailedMessageRetries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FailedMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RetryBatchId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StageAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessageRetries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FailedMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniqueMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProcessingAttemptsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FailureGroupsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryFailureGroupId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MessageType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TimeSent = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SendingEndpointName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceivingEndpointName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExceptionType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExceptionMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QueueAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NumberOfProcessingAttempts = table.Column<int>(type: "int", nullable: true),
                    LastProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConversationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupComments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnownEndpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndpointName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HostDisplayName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Monitored = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LicensingMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Data = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicensingMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageRedirects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ETag = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RedirectsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageRedirects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationsSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailSettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationsSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueueAddresses",
                columns: table => new
                {
                    PhysicalAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FailedMessageCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueAddresses", x => x.PhysicalAddress);
                });

            migrationBuilder.CreateTable(
                name: "RetryBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Context = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetrySessionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StagingId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Originator = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Classifier = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Last = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InitialBatchSize = table.Column<int>(type: "int", nullable: false),
                    RetryType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FailureRetriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetryBatchNowForwarding",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RetryBatchId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryBatchNowForwarding", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetryHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    HistoricOperationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnacknowledgedOperationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MessageTypeTypeName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MessageTypeVersion = table.Column<int>(type: "int", nullable: false),
                    SubscribersJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThroughputEndpoint",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EndpointName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ThroughputSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SanitizedEndpointName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EndpointIndicators = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserIndicator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastCollectedData = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThroughputEndpoint", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrialLicense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    TrialEndDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrialLicense", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveOperations_ArchiveState",
                table: "ArchiveOperations",
                column: "ArchiveState");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveOperations_ArchiveType_RequestId",
                table: "ArchiveOperations",
                columns: new[] { "ArchiveType", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveOperations_RequestId",
                table: "ArchiveOperations",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomChecks_Status",
                table: "CustomChecks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UC_DailyThroughput_EndpointName_ThroughputSource_Date",
                table: "DailyThroughput",
                columns: new[] { "EndpointName", "ThroughputSource", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventLogItems_RaisedAt",
                table: "EventLogItems",
                column: "RaisedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIntegrationDispatchRequests_CreatedAt",
                table: "ExternalIntegrationDispatchRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageRetries_FailedMessageId",
                table: "FailedMessageRetries",
                column: "FailedMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageRetries_RetryBatchId",
                table: "FailedMessageRetries",
                column: "RetryBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_ConversationId_LastProcessedAt",
                table: "FailedMessages",
                columns: new[] { "ConversationId", "LastProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_MessageId",
                table: "FailedMessages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_MessageType_TimeSent",
                table: "FailedMessages",
                columns: new[] { "MessageType", "TimeSent" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_PrimaryFailureGroupId_Status_LastProcessedAt",
                table: "FailedMessages",
                columns: new[] { "PrimaryFailureGroupId", "Status", "LastProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_QueueAddress_Status_LastProcessedAt",
                table: "FailedMessages",
                columns: new[] { "QueueAddress", "Status", "LastProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_ReceivingEndpointName_Status_LastProcessedAt",
                table: "FailedMessages",
                columns: new[] { "ReceivingEndpointName", "Status", "LastProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_ReceivingEndpointName_TimeSent",
                table: "FailedMessages",
                columns: new[] { "ReceivingEndpointName", "TimeSent" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Status_LastProcessedAt",
                table: "FailedMessages",
                columns: new[] { "Status", "LastProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Status_QueueAddress",
                table: "FailedMessages",
                columns: new[] { "Status", "QueueAddress" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_UniqueMessageId",
                table: "FailedMessages",
                column: "UniqueMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupComments_GroupId",
                table: "GroupComments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_LicensingMetadata_Key",
                table: "LicensingMetadata",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetryBatches_RetrySessionId",
                table: "RetryBatches",
                column: "RetrySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RetryBatches_StagingId",
                table: "RetryBatches",
                column: "StagingId");

            migrationBuilder.CreateIndex(
                name: "IX_RetryBatches_Status",
                table: "RetryBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RetryBatchNowForwarding_RetryBatchId",
                table: "RetryBatchNowForwarding",
                column: "RetryBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_MessageTypeTypeName_MessageTypeVersion",
                table: "Subscriptions",
                columns: new[] { "MessageTypeTypeName", "MessageTypeVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UC_ThroughputEndpoint_EndpointName_ThroughputSource",
                table: "ThroughputEndpoint",
                columns: new[] { "EndpointName", "ThroughputSource" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveOperations");

            migrationBuilder.DropTable(
                name: "CustomChecks");

            migrationBuilder.DropTable(
                name: "DailyThroughput");

            migrationBuilder.DropTable(
                name: "EndpointSettings");

            migrationBuilder.DropTable(
                name: "EventLogItems");

            migrationBuilder.DropTable(
                name: "ExternalIntegrationDispatchRequests");

            migrationBuilder.DropTable(
                name: "FailedErrorImports");

            migrationBuilder.DropTable(
                name: "FailedMessageRetries");

            migrationBuilder.DropTable(
                name: "FailedMessages");

            migrationBuilder.DropTable(
                name: "GroupComments");

            migrationBuilder.DropTable(
                name: "KnownEndpoints");

            migrationBuilder.DropTable(
                name: "LicensingMetadata");

            migrationBuilder.DropTable(
                name: "MessageRedirects");

            migrationBuilder.DropTable(
                name: "NotificationsSettings");

            migrationBuilder.DropTable(
                name: "QueueAddresses");

            migrationBuilder.DropTable(
                name: "RetryBatches");

            migrationBuilder.DropTable(
                name: "RetryBatchNowForwarding");

            migrationBuilder.DropTable(
                name: "RetryHistory");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "ThroughputEndpoint");

            migrationBuilder.DropTable(
                name: "TrialLicense");
        }
    }
}
