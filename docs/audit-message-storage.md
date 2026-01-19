# Audit Message Storage Analysis

This document describes how audit messages are stored in the ServiceControl.Audit persistence layer.

## Overview

When an audit message is ingested, ServiceControl.Audit stores the **full message** including headers, body, and enriched metadata. The main document type is `ProcessedMessage`, stored as a RavenDB document.

## What Gets Stored

For each audit message, a `ProcessedMessage` document is created containing:

| Field             | Description                                                                       |
|-------------------|-----------------------------------------------------------------------------------|
| `Id`              | Document ID in format `ProcessedMessages-{processingStartedTicks}-{processingId}` |
| `UniqueMessageId` | Unique identifier extracted from headers                                          |
| `Headers`         | Full NServiceBus message headers dictionary                                       |
| `MessageMetadata` | Dictionary of enriched metadata (~20+ fields)                                     |
| `ProcessedAt`     | Timestamp when message was processed                                              |
| `Body`            | Optional embedded message body (when stored inline)                               |

## Enriched Metadata Fields

The `MessageMetadata` dictionary is populated by enrichers during ingestion:

### Message Type Information

- `IsSystemMessage` - Boolean flag for control messages
- `MessageType` - Full type name of the enclosed message
- `SearchableMessageType` - Message type formatted for full-text search

### Tracking and Correlation

- `ConversationId` - From NServiceBus conversation header
- `RelatedToId` - From NServiceBus related-to header
- `MessageId` - Message identifier
- `MessageIntent` - Send/Publish/Subscribe intent

### Processing Statistics

- `TimeSent` - When the message was sent
- `CriticalTime` - TimeSpan from message send to processing end
- `ProcessingTime` - TimeSpan from processing start to end
- `DeliveryTime` - TimeSpan from send to processing start

### Body Information

- `ContentLength` - Byte size of the message body
- `ContentType` - Media type (e.g., "application/json", "text/plain")
- `BodyUrl` - URL to retrieve the body (`/messages/{messageId}/body`)
- `Body` - Embedded body text (when full-text search enabled and body is small)
- `BodyNotStored` - Flag indicating body was too large to store

### Endpoint Details

- `SendingEndpoint` - EndpointDetails object (Name, Host, HostId)
- `ReceivingEndpoint` - EndpointDetails object (Name, Host, HostId)

## Body Storage Strategy

The body storage uses a **three-tier approach** to balance performance and storage efficiency:

| Condition                     | Storage Location         |
|-------------------------------|--------------------------|
| Body < 85KB AND text-based    | Embedded in the document |
| Body ≥ 85KB OR binary content | RavenDB attachment       |
| Empty body                    | Not stored               |

The 85KB threshold (`LargeObjectHeapThreshold`) is chosen to avoid .NET Large Object Heap allocations.

### Embedded Body Storage

For small text-based messages (JSON, XML, plain text):

- If `EnableFullTextSearchOnBodies` is enabled: stored in `MessageMetadata["Body"]`
- Otherwise: stored in `ProcessedMessage.Body` property

### Attachment Body Storage

For large or binary messages:

- Stored as RavenDB attachments named "body"
- Attachment document ID format: `MessageBodies/{messageId}`

## Key Code Paths

| Step                  | File                               | Description                                 |
|-----------------------|------------------------------------|---------------------------------------------|
| Ingestion entry       | `AuditIngestor.cs`                 | Calls the persister to store messages       |
| Processing            | `AuditPersister.cs`                | Runs enrichers and creates ProcessedMessage |
| Body storage decision | `BodyStorageEnricher.cs`           | Determines where to store body              |
| Document storage      | `RavenAuditIngestionUnitOfWork.cs` | Bulk inserts documents to RavenDB           |
| Attachment storage    | `RavenAttachmentsBodyStorage.cs`   | Stores large bodies as attachments          |

## Data Retention

Documents are stored with expiration metadata based on the configured `AuditRetentionPeriod`. RavenDB automatically removes expired documents.

## Querying

The `MessagesViewIndex` extracts and indexes key fields for efficient querying:

- MessageId, MessageType, TimeSent, ProcessedAt
- ReceivingEndpointName, ConversationId
- CriticalTime, ProcessingTime, DeliveryTime
- Full-text search field combining metadata and header values

## API Access

Message bodies can be retrieved via the REST API endpoint:

```text
GET /messages/{messageId}/body
```

This returns the body content with appropriate content-type headers.

## Performance Considerations

Storing full message bodies impacts ingestion performance:

- **Network I/O** - Large bodies must be transmitted to RavenDB
- **Storage I/O** - RavenDB must write attachment data to disk
- **Memory pressure** - Bodies are held in memory during bulk insert operations
- **Index pressure** - If full-text search is enabled on bodies, indexing large text is expensive

Since audit messages are consumed from message queues (which are transient), body-on-demand retrieval from the source is not possible - the message is gone after consumption.

## IBodyStorage Abstraction

The codebase includes an `IBodyStorage` interface that abstracts body storage operations:

```csharp
interface IBodyStorage
{
    Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken);
    Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken);
}
```

**File:** `src/ServiceControl.Audit.Persistence/IBodyStorage.cs`

### Current Implementations

| Implementation                  | Location                                              | Storage              |
|---------------------------------|-------------------------------------------------------|----------------------|
| `RavenAttachmentsBodyStorage`   | `ServiceControl.Audit.Persistence.RavenDB`            | RavenDB attachments  |
| `InMemoryAttachmentsBodyStorage`| `ServiceControl.Audit.Persistence.InMemory`           | In-memory list       |

### Integration Points

- **RavenDB**: Created per-batch in `RavenAuditUnitOfWorkFactory.cs`
- **InMemory**: Registered as singleton in `InMemoryPersistence.cs`

## Potential Improvements

The `IBodyStorage` abstraction provides an extension point for alternative storage backends:

| Approach                      | Description                                                 | Trade-offs                                      |
|-------------------------------|-------------------------------------------------------------|-------------------------------------------------|
| **External blob storage**     | Store bodies in Azure Blob/S3/filesystem instead of RavenDB | Reduces RavenDB pressure; adds infrastructure   |
| **Configurable body storage** | Setting to disable body storage entirely                    | Maximum throughput; loses body audit capability |
| **Compression**               | Compress bodies before storage                              | CPU trade-off; helps with text-heavy payloads   |

### Implementation Notes

To add an external storage backend:

1. Create new `IBodyStorage` implementation (e.g., `AzureBlobBodyStorage`)
2. Update DI registration in the persistence module
3. **Note:** `RavenAuditDataStore.GetMessageBody()` currently bypasses `IBodyStorage` and fetches directly from RavenDB attachments - this would need refactoring to use the interface
4. Add configuration settings for backend selection and credentials
