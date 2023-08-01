# USAGE:

# To delete the endpoint and all the subqueues, use:
# DeleteQueuesForEndpoint -EndpointName "myendpoint"

# To delete a single queue such as Audit or Error, use:
# DeleteQueue -QueueName "error"

Set-StrictMode -Version 2.0

Add-Type -AssemblyName System.Messaging

Function DeleteQueuesForEndpoint
{
    param(
        [Parameter(Mandatory=$true)]
        [string] $endpointName
    )

    # main queue
    DeleteQueue $endpointName

    # timeout queue
    DeleteQueue ($endpointName + ".timeouts")

    # timeout dispatcher queue
    DeleteQueue ($endpointName + ".timeoutsdispatcher")

    # Msmq storage persistence queue. When using endpointConfiguration.UsePersistence<MsmqPersistence, StorageType.Subscriptions>() in your code, uncomment the following queue or replace it with the configured value.
    # DeleteQueue ($endpointName + ".subscriptions")

}

Function DeleteQueue
{
    param(
        [Parameter(Mandatory=$true)]
        [string] $queueName
    )

    $queuePath = '{0}\private$\{1}'-f [System.Environment]::MachineName, $queueName
    if ([System.Messaging.MessageQueue]::Exists($queuePath))
    {
        [System.Messaging.MessageQueue]::Delete($queuePath)
    }
}
