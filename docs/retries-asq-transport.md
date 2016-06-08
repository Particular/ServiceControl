# How ServiceControl Retries Works with regards to AzureStorageQueue Transport

To get better understanding how retry mechanism works in ServiceControl refer to [How ServiceControl Retries Works] (https://github.com/Particular/ServiceControl/blob/master/docs/bulk-retries-design.md)

## When using one storage account 
 * Both endpoints exist in the same storage account as well as error queue
 * Messages that are failed residing in error queue contains in `FailedQ` header name of the queue of the receiver
 * ServiceControl while doing retry is calling send method with destination set to queue used in `FailedQ` header
 * everything works as expected

## When using multiple storage accounts
This section contains analysis of multiple storage account support when doing retry.

### Each endpoint resides in separate storage account
 * If every account has it's own error queue (that is situated in corresponding storage accounts)
  * An instance per storage account will be required
  * Failed messages can be retried as the queue where it failed is in the same storage account as destination queue
  * Each instance of SC can see only part of the conversation

  
 * If there is one error queue on different storage account
  * ServiceControl need to have only 1 instance connected to storage account that has error queue
  * Failed messages can not be retried at this point of time, as SC would pass the value of FailedQ to Send method as destination. Operation would fail. As the SC uses v6 of ASQ transport in the end it calls:
https://github.com/Particular/NServiceBus.AzureStorageQueues/blob/6.2.1/src/Transport/AzureMessageQueueSender.cs#L41

NOTE: For SC to work FailedQ field need to contains connection name after @.