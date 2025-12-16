using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.Sql.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ArchiveOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RequestId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GroupName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ArchiveType = table.Column<int>(type: "int", nullable: false),
                    ArchiveState = table.Column<int>(type: "int", nullable: false),
                    TotalNumberOfMessages = table.Column<int>(type: "int", nullable: false),
                    NumberOfMessagesArchived = table.Column<int>(type: "int", nullable: false),
                    NumberOfBatches = table.Column<int>(type: "int", nullable: false),
                    CurrentBatch = table.Column<int>(type: "int", nullable: false),
                    Started = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Last = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletionTime = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveOperations", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CustomCheckId = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FailureReason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EndpointName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HostId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Host = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomChecks", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DailyThroughput",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EndpointName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ThroughputSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MessageCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyThroughput", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EndpointSettings",
                columns: table => new
                {
                    Name = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TrackInstances = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndpointSettings", x => x.Name);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EventLogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RelatedToJson = table.Column<string>(type: "json", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EventType = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventLogItems", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ExternalIntegrationDispatchRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DispatchContextJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIntegrationDispatchRequests", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FailedErrorImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MessageJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExceptionInfo = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedErrorImports", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FailedMessageRetries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FailedMessageId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RetryBatchId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StageAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessageRetries", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FailedMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UniqueMessageId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProcessingAttemptsJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureGroupsJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HeadersJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PrimaryFailureGroupId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MessageId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MessageType = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TimeSent = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SendingEndpointName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReceivingEndpointName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExceptionType = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExceptionMessage = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QueueAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NumberOfProcessingAttempts = table.Column<int>(type: "int", nullable: true),
                    LastProcessedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ConversationId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GroupComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    GroupId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Comment = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupComments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "KnownEndpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EndpointName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HostId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Host = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HostDisplayName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Monitored = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownEndpoints", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LicensingMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Key = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Data = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicensingMetadata", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MessageBodies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Body = table.Column<byte[]>(type: "longblob", nullable: false),
                    ContentType = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BodySize = table.Column<int>(type: "int", nullable: false),
                    Etag = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageBodies", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MessageRedirects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ETag = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastModified = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RedirectsJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageRedirects", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NotificationsSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmailSettingsJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationsSettings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "QueueAddresses",
                columns: table => new
                {
                    PhysicalAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailedMessageCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueAddresses", x => x.PhysicalAddress);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RetryBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Context = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RetrySessionId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StagingId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Originator = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Classifier = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Last = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RequestId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InitialBatchSize = table.Column<int>(type: "int", nullable: false),
                    RetryType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FailureRetriesJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryBatches", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RetryBatchNowForwarding",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RetryBatchId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryBatchNowForwarding", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RetryHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    HistoricOperationsJson = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UnacknowledgedOperationsJson = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryHistory", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MessageTypeTypeName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MessageTypeVersion = table.Column<int>(type: "int", nullable: false),
                    SubscribersJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ThroughputEndpoint",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EndpointName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ThroughputSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SanitizedEndpointName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EndpointIndicators = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserIndicator = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Scope = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastCollectedData = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThroughputEndpoint", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
                name: "MessageBodies");

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
