# How ServiceControl retries work with the Azure Storage Queues transport

For how the retry mechanism works in ServiceControl, see [How ServiceControl retries work](https://github.com/Particular/ServiceControl/blob/master/docs/bulk-retries-design.md).

## When using one storage account

* Both endpoints and the error queue exist in the same storage account.
* A failed message in the error queue contains the queue name of the receiver in the `FailedQ` header.
* During a retry, ServiceControl calls the send method with the destination set to the queue in the `FailedQ` header.
* Retries work as expected.

## When using multiple storage accounts

This section analyzes multiple storage account support for retries.

### Each endpoint resides in a separate storage account

* If every account has its own error queue, located in the same storage account:
  * One ServiceControl instance per storage account is required.
  * Failed messages can be retried, because the queue where the message failed is in the same storage account as the destination queue.
  * Each ServiceControl instance sees only part of the conversation.

* If there is one error queue in a different storage account:
  * ServiceControl needs only one instance, connected to the storage account that has the error queue.
  * Failed messages cannot be retried at this point in time. ServiceControl passes the value of `FailedQ` to the send method as the destination, and the operation fails. ServiceControl uses v6 of the ASQ transport, which ends in a call to
https://github.com/Particular/NServiceBus.AzureStorageQueues/blob/6.2.1/src/Transport/AzureMessageQueueSender.cs#L41

NOTE: For ServiceControl to work, the `FailedQ` field needs to contain the connection name after `@`.
