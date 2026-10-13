# How ServiceControl retries work

## Overview

When you request a retry (bulk or individual) using the ServiceControl API, ServiceControl creates one or more retry batches. Each batch goes through a series of steps that send the matching failed messages back to their intended destinations.

A retry batch contains at most 1,000 failed messages. ServiceControl splits larger requests into several batches of at most 1,000.

Batches go through four stages in order: `Marking Documents` -> `Staging` -> `Forwarding` -> `Done`

The code that handles each stage is idempotent, so re-processing a batch is safe. A batch only moves forward through the stages. A message picked up in the first stage is eventually retried.

ServiceControl also keeps an in-memory representation of the retry operation for progress tracking. It is created when the request is received and updated as the retry batches of the operation move through the stages. When the operation completes, ServiceControl persists a history item so that users can view finished operations.

When ServiceControl restarts, it rebuilds the in-memory representations by aggregating state from the persisted batches.

### Marking Documents

A retry batch has this state when it is first created. In this state, ServiceControl finds failed messages and marks them as belonging to the batch.

ServiceControl creates a new document for each failed message. Each document has the id `FailedMessageRetry/{messageId}` and contains the failed message id and the retry batch id. Only one document with this key can exist at a time, so a failed message can belong to only one batch. The `FailedMessageRetry` document exists until a new failed message with the same id comes through the error queue. This guarantees that a failed message has only one outstanding retry at a time.

When all messages are marked, ServiceControl appends a list of the `FailedMessageRetry` ids to the batch and changes the batch status to `Staging`. The list in the batch can contain failed messages that do not belong to this batch, because another batch claimed them in parallel. Staging filters these out (see below).

When ServiceControl starts up, it adopts any batch it finds in the `Marking Documents` status and moves it to `Staging`. This happens only if the ServiceControl process stopped during marking. ServiceControl adds the documents that were already marked to the batch. It ignores documents that were not yet marked, and the user has to retry those again.

When a bulk retry is issued, ServiceControl starts a background process. The process looks for all messages that match the criteria and assigns them to a retry batch id, in lots of 1,000. If the process stops before it finishes, the details of the requested retry are lost. Documents already marked for retry are retried. Messages that were not marked require the user to retry them manually.

When ServiceControl starts up, it generates a `Session ID` GUID. ServiceControl stamps this GUID onto each new batch as it creates the batch. This is how ServiceControl tells that a batch is from a previous session. The orphan batch process adopts only batches with a non-current session id.

## Recovering retry batches

When ServiceControl starts, it looks for documents marked with a retry batch id that have no matching retry batch document. This happens when the process started making the batch but never completed it. ServiceControl calls such batches "orphaned" because the process that was assembling them is lost.

When an instance of ServiceControl "adopts" an orphaned batch, it takes over the processing. The instance finds all messages with the same missing retry batch id and creates a retry batch with that id for them. ServiceControl then processes this new retry batch document like any other.

### Staging

Staging reduces the chance of sending a message for retry more than once. Transports that do not support `SendsAtomicWithReceive` strictly would not need staging to prevent more-than-once delivery.
In this state, ServiceControl adds the failed messages of the batch to a special `staging` queue. `Forwarding` uses this queue to send messages back to their original destination transactionally, using the receive transaction of the transport.

Multiple batches can be in `Staging` or `Forwarding` at a time, but a single thread processes them, which serializes the rest of the process. Only one batch at a time can have access to the `staging` queue. ServiceControl processes batches in `Staging` only if no batch is in `Forwarding`.

If a message fails to be forwarded, ServiceControl removes it from the batch. If the batch then contains no messages, ServiceControl marks it as complete.

When ServiceControl selects a batch for staging, it generates a new `Staging Id` for the batch and loads the list of failed messages that belong to it. At this point, ServiceControl filters out any message that another batch claimed.

ServiceControl dispatches each message to the `staging` queue, one at a time. For each message, it does the following:

1. Updates the corresponding `Failedmessage` document to the `RetryIssued` mode.
2. Strips the error headers from the copy sent to `staging`.
3. Adds a header to the copy sent to `staging` that stamps it with the `Staging Id`.
4. Adds a header to the copy sent to `staging` that indicates the final destination of the message.

When all messages are staged, ServiceControl adds the final count of staged messages to the batch and updates the batch status to `Forwarding`.

If staging fails part way through, the `staging` queue contains messages, but ServiceControl does not know which ones. The `Staging Id` solves this. ServiceControl saves the `Staging Id` and updates the status to `Forwarding` at the same time, and a single thread does this work. Therefore only one `Staging Id` reaches the `Forwarding` state. When forwarding starts, ServiceControl sends only messages with a matching `Staging Id`. A message with a different `Staging Id` is from a previous staging attempt and ServiceControl discards it.

When ServiceControl moves the batch into `Forwarding`, it also records the batch id in a Raven document with a well-known id (`RetryBatches/NowForwarding`). This avoids a query, and potentially stale indexes from Raven, when ServiceControl checks whether a batch is in `Forwarding`. Only one batch can be in `Forwarding` at any time, because forwarding clears the contents of the `staging` queue.

### Forwarding

A batch in this status has all of its failed messages in the `staging` queue, and ServiceControl can start sending them to their final destination. Forwarding runs in one of two modes: counting and non-counting. Counting is the standard mode.

A satellite is attached to the `staging` queue and can be turned on and off. When ServiceControl finds a batch with the `Forwarding` status, it turns on the satellite and passes in the `Staging Id` and `Message Count` of the batch. The satellite checks that each received message has a matching `Staging Id`. If it does, the satellite forwards the message to its final destination and increments an internal counter. When the counter reaches `Message Count`, the entire batch is forwarded.

Each message send happens in the context of a satellite receive operation, so the process should use the native transactions of the transport.

If a message is in the `Forwarding` status when ServiceControl starts, ServiceControl does not know how many messages are still in the staging queue. In that case, ServiceControl turns on the satellite in non-counting mode, which runs until the queue is empty. The transport abstraction cannot query the queue length. ServiceControl therefore assumes that the queue is empty if it sees no new messages from the `staging` queue within 45 seconds.

If forwarding a specific message fails, ServiceControl counts it, marks it as unresolved, and removes it from the batch. The log then contains a warning like this, with the error information attached:

> Failed to send UNIQUE-MESSAGE-ID message to DESTINATION for retry. Attempting to revert message status to unresoved so it can be tried again.

If ServiceControl crashes immediately after forwarding completes, it finds an empty staging queue after the restart and times out.

### Done

No status indicates that a batch is `Done`. When the `Forwarding` status completes, ServiceControl deletes the batch. Each message retried as part of the batch still has a corresponding `FailedMessageRetry/{messageId}` document, which prevents ServiceControl from retrying the message again.

When all batches of a retry operation complete, ServiceControl adds two entries to a retry history document. ServiceControl keeps the "unacknowledged" entry until a user acknowledges the completion of the operation. The other entry shows users the historic retry operations.

## Other notes

1. A message can be part of only one batch at a time. The `FailedMessageRetry/{messageId}` document prevents a message from being added to a second batch. ServiceControl removes this document only when the message comes back through the error queue.
2. A batch that is created is eventually forwarded. If the ServiceControl process stops while the batch is in `Marking Documents`, the Adopt Orphan Batches process picks it up and moves it into `Staging`. Once a batch is in `Staging`, the Retry Processor repeatedly attempts to stage it until it succeeds. ServiceControl then selects the batch for `Forwarding`.
3. ServiceControl processes only one batch at a time in the `Forwarding` or `Staging` status. A batch in `Forwarding` must be fully forwarded and deleted before ServiceControl can stage a new batch. A batch selected for staging moves to the `Forwarding` status once it is fully staged. All of this happens on a single background thread that sleeps for 30 seconds if it finds nothing to do.
4. A batch can be forwarded only if it is completely staged.
5. If an attempt to stage a batch is interrupted, the next attempt stages the entire batch again. Each staging attempt has its own `Staging Id`, so only one staging attempt reaches `Forwarding`. The `Forwarding` process drops any messages from a previous attempt. This makes the staging step idempotent.
6. If an attempt to forward a batch is interrupted, the next attempt forwards the matching staged messages until the staging queue is empty. During this phase, ServiceControl drops any message that does not match the recorded `Staging Id`. When the staging queue is empty, every previously staged message was sent. This makes the forwarding step idempotent.
7. ServiceControl receives each message of a forwarding operation from the staging queue and dispatches it to its final destination as part of the same transport transaction. If a message is received but cannot be forwarded, the receive should be rolled back. The satellite that handles forwarding includes a custom fault manager that attempts to eject the failed message from the batch. In this case, a message can be retried multiple times.

### Technicalities of retrying messages

- The `Headers.FailedQ` header is used as the new destination of the message.
  - ServiceControl populates `FailedQ` with the name of the queue to send the message back to.
- ServiceControl strips the following headers from the original message headers:
  - `NServiceBus.Retries`
  - `NServiceBus.FailedQ`
  - `NServiceBus.TimeOfFailure`
  - `NServiceBus.ExceptionInfo.ExceptionType`
  - `NServiceBus.ExceptionInfo.AuditMessage`
  - `NServiceBus.ExceptionInfo.Source`
  - `NServiceBus.ExceptionInfo.StackTrace"`
- ServiceControl sends the message to the above destination by calling the `ISendMessages.Send` implementation of the transport.
