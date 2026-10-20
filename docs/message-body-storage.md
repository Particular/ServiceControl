# Message body storage

The SQL Server and PostgreSQL persisters for ServiceControl.Audit keep every message body. A small valid text body stays in the database row. Any other body goes to external storage: a directory, an Azure Blob container, or an S3 bucket. The providers live in `src/ServiceControl.Persistence.BodyStorage` and are shared with the Error (primary) persisters.

## Placement rules

`MaxBodySizeToStore` (default 100 KB) is the inline threshold. It does not limit the size of a stored body.

| Body | Where it goes | `BodyState` | Inline `BodyText` |
| --- | --- | --- | --- |
| Empty | Nowhere | `None` | none |
| Valid UTF-8 text, at most `MaxBodySizeToStore` bytes | Database row | `Stored` | whole body |
| Valid UTF-8 text, over `MaxBodySizeToStore` bytes | External storage | `External` | first `MaxBodySizeToStore` bytes, as a search prefix |
| Binary content type, `Content-Encoding` header, invalid UTF-8, or a NUL character | External storage | `External` | none |

External storage always holds the complete body. Only the inline prefix is searchable, as before.

Rows written before external storage existed keep their meaning: `TooLarge` answers 204 and `NotText` answers 404. The bodies behind those rows were discarded at ingestion and cannot be recovered. `External` is a new value of the existing integer column, so there is no schema change.

## Ordering and failures

Ingestion writes external bodies before the batch transaction that inserts the rows. A failed write fails the batch, and no row is inserted. A failed insert can leave an unreferenced body.

A body is stored under its audit body id, `yyyyMMddHH-{unique message id}`, the same id used in the body URL. Storage keeps the first write of an id and ignores later writes.

Reading a body streams it from storage with the stored content type, size, and a version derived from the row. A body missing from storage answers 204.

## Retention

Retention removes bodies before it removes rows. For each expired unit it calls `IBodyStoragePersistence.DeleteBodiesWithPrefix`, and it drops the rows only after that call succeeds. If storage fails, the rows and partitions stay and the next sweep retries.

The unit differs by provider, so the prefix is part of `IAuditPartitionManager`:

- SQL Server deletes rows one hour at a time. The prefix is the hour, `yyyyMMddHH-`.
- PostgreSQL drops whole day partitions without scanning rows. The prefix is the day, `yyyyMMdd`, and it matches every hour of that day. A day is only dropped once all of it is older than the retention period, so no body is removed earlier than its row would be.

Prefixes must not be empty and must not contain wildcard or path characters. SQL Server only visits hours that have rows, so an unreferenced body in an hour with no rows stays until that location is cleaned manually. PostgreSQL visits every expired day partition, including pre-created partitions without rows, so it also cleans unreferenced bodies of those days.

## Configuration

External storage is required. The instance fails to start when `MessageBody/StorageType` is missing, which includes existing SQL Server and PostgreSQL Audit instances that upgrade without adding it. Setup creates the directory, container, or bucket.

Settings use the `ServiceControl.Audit/` prefix in the application settings file. The validation matches the Error instance, whose settings use the `ServiceControl/` prefix.

| Key | Applies to | Default |
| --- | --- | --- |
| `MessageBody/StorageType` | all, required: `FileSystem`, `AzureBlob`, or `S3` | none |
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

Give each Audit instance its own directory, container, or key prefix, and never share one with an Error instance. Retention deletes by id prefix, and the prefixes of two instances can match the same hours. An instance that shares a location can delete bodies that another instance still references. The Audit defaults differ from the Error defaults (`error-bodies`, `error-bodies/`) for this reason, but they are the same for every Audit instance, so a second Audit instance in the same account must set its own container name or key prefix.

With `FileSystem` storage, an instance registers the `ServiceControl body storage` custom check that reports low free space on the drive.
