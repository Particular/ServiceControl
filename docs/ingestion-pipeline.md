# Ingestion pipeline

Both instances take messages off a queue, batch them, and write each batch to storage. That
machinery is one class, `IngestionPipeline` in `ServiceControl.Infrastructure`, used by
`ErrorIngestion` and `AuditIngestion`. This document covers how it behaves, what can be tuned, and
why the answer to "can batches be written in parallel" belongs to the storage rather than to the
instance.

For what the error instance then does with a batch, see [error-ingestion-design.md](error-ingestion-design.md).
For the metrics it publishes, see [telemetry.md](telemetry.md).

## Shape

```
transport receivers  ──►  message channel  ──►  batch assembler  ──►  batch channel  ──►  writers
   (MaxConcurrency)         (bounded)            (single reader)        (bounded)         (1..n)
```

A receiver calls `Enqueue` and then waits on the message's `TaskCompletionSource`. That is what
makes the receive commit only after the message has been written: nothing is acknowledged to the
broker until the batch it landed in is in storage. A failed batch faults the completion sources of
its own messages and nothing else, so the transport redelivers exactly those.

Both channels are bounded. When storage slows down, the batch channel fills, then the message
channel fills, and then the receivers block in `Enqueue`. Back pressure reaches the broker instead
of a queue growing in memory.

The assembler is the only reader of the message channel, which is what keeps several writers fed
without any of them competing for messages.

### Shutdown

`StopAsync` stops receiving first, under the shutdown token rather than a cancelled one, so
messages already in flight finish and their receives commit. It then completes the pipeline and
waits for it to drain, and only then tears the transport infrastructure down, because batches
still forward through its dispatcher.

A hard cancellation does not silently drop what is in flight. The assembler abandons the batch it
was building and whatever is left in the message channel, any batch no writer picked up is
abandoned, and a writer fails the batch it was holding. Every one of those receives is answered,
so they are redelivered rather than left waiting for a shutdown that has already happened.

## Settings

Named per instance, and read from that instance's settings root
(`ServiceControl/...` and `ServiceControl.Audit/...`).

| Setting | Default | Range | What it does |
| --- | --- | --- | --- |
| `ErrorIngestionBatchSize` / `AuditIngestionBatchSize` | the transport's `MaximumConcurrencyLevel` | 1 to 1000 | The most messages one write handles |
| `ErrorIngestionMaxParallelWriters` / `AuditIngestionMaxParallelWriters` | the storage decides, see below | 1 to 16 | How many batches are written at once |
| `ErrorIngestionBatchTimeout` / `AuditIngestionBatchTimeout` | `00:00:00` | 0 to 5 seconds | How long a batch that is not yet full waits for more messages |

As environment variables:

```bash
SERVICECONTROL_ErrorIngestionBatchSize=50
SERVICECONTROL_ErrorIngestionMaxParallelWriters=8
SERVICECONTROL_ErrorIngestionBatchTimeout=00:00:00.100

SERVICECONTROL_AUDIT_AuditIngestionBatchSize=50
SERVICECONTROL_AUDIT_AuditIngestionMaxParallelWriters=8
SERVICECONTROL_AUDIT_AuditIngestionBatchTimeout=00:00:00.100
```

### Batch size

The default, the transport's concurrency, is also the ceiling. A receive does not return until the
batch carrying its message has been written, so the transport holds every one of its concurrency
slots open and no more than that many messages can ever be waiting. Setting a batch size above the
transport's concurrency is therefore unreachable, with or without a batch timeout: the batch never
fills, and a timeout only delays what has already arrived. Larger batches come from raising
`MaximumConcurrencyLevel`, and this setting is what holds writes below it when a storage is happier
with smaller ones.

### Batch timeout

Zero means a partial batch is written rather than waited on, which is what the ingestion did before
the setting existed. A non-zero value trades latency for fewer, larger writes: at volume it costs
nothing, because a full batch never waits, and at a trickle it delays each message by up to the
timeout. Start at 100ms if a storage is clearly happier with larger batches. It cannot make a batch
larger than the transport's concurrency, only fuller.

### Parallel writers

Only raise this for a storage whose writes are safe to interleave. Batches commit in whatever order
they finish, so this is not a free throughput knob, and the pipeline will not let you turn it on
where it is unsafe.

More than one writer also means more than one batch is being enriched and announced at a time, so a
custom `IEnrichImportedErrorMessages` or `IEnrichImportedAuditMessages` has to be thread safe. A
single writer used to serialise them.

## Which storages take concurrent batches

Each ingestion unit of work factory answers `SupportsConcurrentBatches`. Where it says no, the
pipeline uses one writer whatever is configured, and logs a warning if that overrules a setting
someone actually asked for rather than a default.

| Storage | Concurrent batches | Why |
| --- | --- | --- |
| Error, SQL Server and PostgreSQL | yes | The batch writer was built for it: upserts guarded by the attempt times so the newer attempt wins whichever transaction commits last, inserts that tolerate a competing writer's identical row, and a consistent lock order. Running several `--error-ingestion-only` hosts against one database already depends on all of it. |
| Error, RavenDB | no | Failed messages are merged by patch scripts that read and rewrite one document, and nothing orders two patches of the same document against each other. `--error-ingestion-only` refuses to start on RavenDB for the same reason. |
| Audit, RavenDB | yes | Audit documents are independent. Nothing merges two of them, and every batch gets its own bulk insert operation. |
| Audit, in memory | no | Test storage only. |

A storage that says yes gets four writers by default.

### The ordering that concurrency does not excuse

Concurrent writers mean two batches touching the same message can commit in either order, which is
the same condition several ingestion hosts already create. Storage is responsible for making the
end state the same either way, and for failed messages that means comparing the times the events
happened rather than trusting arrival order:

- a retry acknowledgement resolves a message only if no attempt newer than the retry has been
  stored, because such an attempt means the message failed again afterwards
- an attempt moves the status only if it is strictly newer than the newest attempt already stored,
  so a redelivery of the attempt already there cannot undo a resolve or an archive

## Where things live

| | |
| --- | --- |
| `ServiceControl.Infrastructure/Ingestion/IngestionPipeline.cs` | The channels, the assembler and the writers |
| `ServiceControl.Infrastructure/Ingestion/IngestionSettingsReader.cs` | Reading, validating and resolving the settings above |
| `ServiceControl.Infrastructure/Ingestion/Metrics/` | The metric scopes both instances report through |
| `ServiceControl/Operations/ErrorIngestion.cs` | Transport, watchdog and fault policy for the error queue |
| `ServiceControl.Audit/Auditing/AuditIngestion.cs` | The same for the audit queue |
