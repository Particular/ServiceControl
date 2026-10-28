# Message body storage

The SQL Server and PostgreSQL persisters for ServiceControl.Audit can keep every message body. A small valid text body stays in the database row. With external storage configured, any other body goes there: a directory, an Azure Blob container, or an S3 bucket. Without it, the instance discards bodies over the size limit, as RavenDB does, and bodies that cannot be stored as text. The providers live in `src/ServiceControl.Persistence.BodyStorage` and are shared with the Error (primary) persisters.

## Placement rules

`MaxBodySizeToStore` (default 100 KB) is the inline threshold. With external storage configured, it does not limit the size of a stored body. The table describes that case; the next section describes the case without it.

| Body | Where it goes | `BodyState` | Inline `BodyText` |
| --- | --- | --- | --- |
| Empty | Nowhere | `None` | none |
| Valid UTF-8 text, at most `MaxBodySizeToStore` bytes | Database row | `Stored` | whole body |
| Valid UTF-8 text, over `MaxBodySizeToStore` bytes | External storage | `External` | first `MaxBodySizeToStore` bytes, as a search prefix |
| Binary content type, `Content-Encoding` header, invalid UTF-8, or a NUL character | External storage | `External` | none |

External storage always holds the complete body. Only the inline prefix is searchable, as before.

Rows written before external storage existed keep their meaning: `TooLarge` answers 204 and `NotText` answers 404. The bodies behind those rows were discarded at ingestion and cannot be recovered. `External` is a new value of the existing integer column, so there is no schema change.

## Without external storage

When `MessageBody/StorageType` is not set, nothing is written outside the database and startup needs no provider. Valid UTF-8 text within `MaxBodySizeToStore` stays inline as above. Text over the limit is dropped with the same inline search prefix (`TooLarge`, answers 204). Other bodies are dropped (`NotText`, answers 404; a binary body over the limit is `TooLarge`).

Removing the storage settings from an instance that used them leaves its `External` rows in place. Their bodies answer 204, and retention no longer deletes them from the old location. Clean that location manually.

## Ordering and failures

Ingestion writes external bodies before the batch transaction that inserts the rows. A failed write fails the batch, and no row is inserted. A failed insert can leave an unreferenced body.

Audit stores a body through `IAuditBodyStoragePersistence` under the UTC ingestion hour of its batch and the unique message id: `yyyyMMddHH/{unique message id}`. The file system store writes `{StoragePath}/yyyyMMddHH/{unique message id}.body`, the Azure Blob store uses the blob name `yyyyMMddHH/{unique message id}`, and the S3 store prepends its key prefix. Callers pass the hour explicitly and the stores do not parse it from the id, so reads use the `CreatedOn` of the row. Storage keeps the first write for an hour and id and ignores later writes.

The body URL keeps the audit body id, `yyyyMMddHH-{unique message id}`, which routes the request to the row. The Error (primary) persisters use `IBodyStoragePersistence` with the body id alone; their file system layout remains `{StoragePath}/{body id}.body`.

Reading a body streams it from storage with the stored content type, size, and a version derived from the row. A body missing from storage answers 204.

## Retention

Retention removes bodies before it removes rows. For each expired unit it calls `IAuditPartitionManager.DeleteBodies`, and it drops the rows only after that call succeeds. If storage fails, the rows and partitions stay and the next sweep retries.

The unit differs by provider, so the delete call is part of `IAuditPartitionManager`:

- SQL Server deletes rows one hour at a time and calls `DeleteBodiesForHour`.
- PostgreSQL drops whole day partitions without scanning rows and calls `DeleteBodiesForDay`, which covers every hour of that day. A day is only dropped once all of it is older than the retention period, so no body is removed earlier than its row would be.

The file system store deletes the hour directory recursively, so it also removes leftover temporary files in that directory. The day call deletes the 24 hour directories of that day without listing the storage root. The Azure Blob store lists the blobs of the hour or day and deletes them in batches of at most 256. A batch is not atomic, so the store checks the status of every delete, treats a missing blob as deleted, and fails the call on any other error. The S3 store deletes objects in batches of up to 1,000 and checks for per-object failures.

SQL Server only visits hours that have rows, so an unreferenced body in an hour with no rows stays until that location is cleaned manually. PostgreSQL visits every expired day partition, including pre-created partitions without rows, so it also cleans unreferenced bodies of those days.

## Configuration

External storage is optional for Audit instances using SQL Server or PostgreSQL. An Audit instance without `MessageBody/StorageType` starts without it. External storage remains required for Error (primary) instances using these persisters; they fail to start without `ServiceControl/MessageBody/StorageType`. See [Error body placement](error-ingestion-design.md#body-placement).

An invalid value, or missing settings for the chosen type, still fails at startup. Setup creates the directory, container, or bucket.

Audit settings use the `ServiceControl.Audit/` prefix in the application settings file. Provider-specific validation matches the Error instance, whose settings use the `ServiceControl/` prefix.

| Key | Applies to | Default |
| --- | --- | --- |
| `MessageBody/StorageType` | all, optional: `FileSystem`, `AzureBlob`, or `S3` | none |
| `MessageBody/MinCompressionSize` | all, minimum body size in bytes to compress | 4096 |
| `MessageBody/FileSystem/StoragePath` | `FileSystem`, required | none |
| `MessageBody/FileSystem/DataSpaceRemainingThreshold` | `FileSystem`, free space percentage below which the custom check fails | 15 |
| `MessageBody/Azure/ConnectionString` | `AzureBlob`, shared key or SAS | none |
| `MessageBody/Azure/ServiceUri` | `AzureBlob`, managed identity | none |
| `MessageBody/Azure/ManagedIdentityClientId` | `AzureBlob`, managed identity | none |
| `MessageBody/Azure/AuthorityHost` | `AzureBlob`, managed identity | SDK default |
| `MessageBody/Azure/ContainerName` | `AzureBlob` | `audit-bodies` |
| `MessageBody/S3/BucketName` | `S3`, required | none |
| `MessageBody/S3/KeyPrefix` | `S3` | `audit-bodies/` |
| `MessageBody/S3/Region` | `S3` | SDK default |
| `MessageBody/S3/ServiceUrl` | `S3` | SDK default |
| `MessageBody/S3/AccessKeyId`, `MessageBody/S3/SecretAccessKey` | `S3`, set both or neither | default AWS credential chain |

Azure requires exactly one of the connection string and the service URI. `MaxBodySizeToStore` keeps its existing top-level setting.

```xml
<add key="ServiceControl.Audit/MessageBody/StorageType" value="FileSystem" />
<add key="ServiceControl.Audit/MessageBody/FileSystem/StoragePath" value="D:\ServiceControl.Audit\bodies" />
```

```xml
<add key="ServiceControl.Audit/MessageBody/StorageType" value="S3" />
<add key="ServiceControl.Audit/MessageBody/S3/BucketName" value="my-bodies" />
<add key="ServiceControl.Audit/MessageBody/S3/KeyPrefix" value="audit-instance-1/" />
```

## One location per instance

Give each Audit instance its own directory, container, or key prefix, and never share one with an Error instance. Retention deletes whole hour or day groups, so a shared location lets one instance delete bodies that another still references. The Audit defaults differ from the Error defaults (`error-bodies`, `error-bodies/`) for this reason, but they are the same for every Audit instance, so a second Audit instance in the same account must set its own container name or key prefix.

With `FileSystem` storage, an instance registers the `ServiceControl body storage` custom check that reports low free space on the drive.
