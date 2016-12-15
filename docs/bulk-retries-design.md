# How ServiceControl Retries Works

## Overview

When you request a Retry (Bulk or Individual) using the ServiceControl API, ServiceControl creates one (or more) Retry Batches which go through a series of steps to ensure that matching Failed Messages get sent back to their intended destinations.

Each Retry Batch consists of no more than 1000 failed messages. Anything bigger than that gets broken up into smaller Retry Batches.

Batches go through 4 stages in order: `Marking Documents` -> `Staging` -> `Forwarding` -> `Done`

The code that handles each stage is idempotent so re-processing a batch is never a problem. As a batch can only ever move forward through the stages, if a message is picked up in the first stage it should eventually be retried. 

### Marking Documents

A `RetryOperation` is created as an encompassing object to represent that entire set of batches within a retry for a group. This retry operation records the number of batches that comprised the original group as well as the number of batches that still remain to be completed. 

When a retry batch is first created it has state `Marking Documents`. This means that ServiceControl is finding and marking failed messages as belonging to this batch.

To do this, a new document is created for each failed message. Each one has an id `FailedMessageRetry/{messageId}` and contains the failed message id and the retry batch id. As only one document with this key can exist at a time, this ensures that a Failed Message can only ever belong to a single batch. This document will exist until such a time as a new Failed Message with same Id comes through the error queue. This guarantees that there can only be one outstanding retry for a failed message at a time.

When all messages have been marked, a list of the `FailedMessageRetry` ids is appended to the batch and the batch changes status to `Staging`. The list inside of the Batch may contain failed messages which do not belong to this batch (because another batch claimed them in parallel). These will get filtered out during staging (below).

When ServiceControl starts up it will attempt to adopt any batches that it finds in this status and move them to `Staging`. This can only happen if the SC process stops during the above process. Any documents that were already marked will be added to the batch. Any documents that had not yet been marked are ignored and will have to be retried again by the user. When SC starts up, it will generate a `Session ID` GUID. This GUID is stamped onto each new batch as it is created. This is how SC can tell if a batch is from a previous session and adopt it. Only Batches with a non-current session Id will be adotped by the orphan batch process. There is a possibility of zombie `RetryOperation` documents at this stage.

### Staging

When a batch enters this state, it means that failed messages belonging to the batch are being added to a special `staging` queue. This queue is used during `Forwarding` to ensure that messages are sent back to their original destination transactionally (using the transports recieve transaction).

NOTE: Although multiple batches may be in `Staging` or `Forwarding` at a time, these are processed by a single thread ensuring that the rest of the process is serialized. This is important to ensure that only one batch at a time has access to the `staging` queue. Batches in `Staging` will only be processed if there are no batches in `Forwarding`.

When a batch is selected for staging a new `Staging Id` is generated for it and the list of failed messages belonging to the batch is loaded. At this time, if a message had been snagged by another batch, it gets filtered out.

Each message, one at a time, is dispatched to the `staging` queue. As each message is dispatched:
1. It's Raven document is updated to reflect that it has entered `RetryIssued` mode.
2. Error headers are stripped from the copy sent to `staging`
3. A header is added to the copy sent to `staging` to stamp it with the `Staging Id`
4. A header is added to the copy sent to `staging` to indicate the messages final destination

Once all messages have been staged, a final count of the messages that have been staged gets added to the batch, and the batches status is updated to `Forwarding`.

NOTE: If this process fails part way through, there will be messages on the `staging` queue but we won't be sure which ones. This is the purpose of the `Staging Id`. By saving the `Staging Id` and updating the status to `Forwarding` at the same time (and because this process happens on a single thread), we guarantee that only one `Staging Id` will make it to the `Forwarding` state. When we start forwarding messages, we will only send ones with a matching `Staging Id`. If the `Staging Id` does not match then it is from a previous staging attempt and can be safely discarded. 

NOTE: On top of moving the batch into `Forwarding` we also record it's Id in a Raven document with a well known Id (`RetryBatches/NowForwarding`). We do this to avoid a query and potentially stale indexes from Raven when we want to check if there is a batch in `Forwarding`. It is imperative that only one batch is ever in `Forwarding` at any given time. This is because it will clear out the contents of the `staging` queue.   

### Forwarding
Once a batch reaches this status it means that all of the failed messages that are a part of the batch are in the `staging` queue and we can start sending them to their final destination. This can happen in one of two modes: Counting and not-Counting. Counting is the standard mode of operation.

There is a Satellite attached to the `staging` queue which can be turned on and off. When a batch is found with the `Forwarding` status we turn on the satellite and pass in the `Staging Id` and `Message Count` of the batch. Each message that is received by the satellite will be checked to ensure that it has a matching `Staging Id`. If it does, then it is forwarded to it's final destination and an internal counter is incremented. If the internal counter reaches `Message Count` for the batch then the entire batch has been forwarded.  

Because each message send is done in the context of a Satellite Receive operation, this process should utilize the transports native transactions.

If there is a message in the `Forwarding` status when ServiceControl starts, then we don't know how many messages there are still in the staging queue to send. To counter this we turn on the satellite in Non-Counting mode. The idea for this is that the satellite will run until the queue is empty. Unfortunately there is nothing built into the Transport abstraction that allows us to query this so SC assumes that if it does not see any new messages from the `staging` queue within 45 seconds then it is empty.

### Done
There is no status to indicate that a batch is `Done`. When the `Forwarding` status is completed, the batch is deleted as it is no longer relevant. Note that each message that was retried as a part of the batch still have a corresponding `FailedMessageRetry/{messageId}` document. This will prevent the message from being retried again.

## Other notes
1. A message can only be a part of one batch at a time. The `FailedMessageRetry/{messageId}` document will prevent a message from being added to a second batch. This document will only be removed if we see the message coming back through the error queue.
2. Once a batch is created it will eventually be forwarded. If the SC process dies while the batch is in `Marking Documents` then it will be picked up by the Adopt Orphan Batches process which will move it into `Staging`. Once a batch is in `Staging` the Retry Processor will repeatedly attempt to stage it until successful at which point it will be selected for `Forwarding`.
3. Only one batch at a time will be processed in the `Forwarding` or `Staging` status. If there is a batch in `Forwarding` then it must be fully forwarded and deleted before a new batch can be staged. If a batch is selected to be staged then it will move to the `Forwarding` status once it is fully staged. All of this happens on a single background thread that will sleep for 30 seconds if it can't find anything to do.
4. A batch can only be forwarded if it has been completely staged
5. If an attempt to stage a batch is interrupted, the next attempt will result in the entire batch being staged again. As each staging attempt has it's own `Staging Id`, only one staging attempt will make it to `Forwarding`. Any messages from a previous attempt will be dropped as a part of the `Forwarding` process. This makes the staging step idempotent.
6. If an attempt to forward a batch is interrupted, the next attempt will simply forward matching staged messages until the staging queue is empty. During this phase, any message that does not match the recorded `Staging Id` is dropped. When the staging queue is empty, every previously staged message must have been sent. This makes the forwarding step idempotent.
7. Each message that is sent as a part of a forwarding operation is Received from the staging queue and Dispatched to it's final destination as a part of the same Transport Transaction. If a message is recieved but cannot be forwarded then the recieve should be rolled back. The satellite that handles forwaring includes a custom Fault Manager that will attempt to eject the failed message from the batch. Under this circumstance, it is possible for a message to be retried multiple times. 

### Technicalities of retrying messages
 * `Headers.FailedQ` header is used as a new destination of the message.
  * FailedQ is populated with the queue name to send the message back to.
 * The original message headers are striped from following header values: 
  * `NServiceBus.Retries`
  * `NServiceBus.FailedQ`
  * `NServiceBus.TimeOfFailure`
  * `NServiceBus.ExceptionInfo.ExceptionType`
  * `NServiceBus.ExceptionInfo.AuditMessage`
  * `NServiceBus.ExceptionInfo.Source`
  * `NServiceBus.ExceptionInfo.StackTrace"`

 * Message is sent using above destination calling `ISendMessages.Send` implementation of given transport. 
