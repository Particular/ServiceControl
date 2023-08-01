# USAGE:

# To create the endpoint and all the subqueues, use:
# CreateQueuesForEndpoint -EndpointName "myendpoint" -Account $env:USERNAME

# To create a common queue for all endpoints such as Audit or Error, use:
# CreateQueue -QueueName "error" -Account $env:USERNAME
# CreateQueue -QueueName "audit" -Account $env:USERNAME

Set-StrictMode -Version 2.0
Add-Type -AssemblyName System.Messaging

# Helper functions
Function CreateQueue
{
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $QueueName,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [ValidateScript({ValidateAccount -Account $_})]
        [string] $Account
    )

    $queuePath = '{0}\private$\{1}' -f $env:COMPUTERNAME, $QueueName

    if (-Not [System.Messaging.MessageQueue]::Exists($queuePath)) {
        $messageQueue = [System.Messaging.MessageQueue]::Create($queuePath, $true)
        SetDefaultPermissionsForQueue -Queue $messageQueue -Account $Account
    }
    else {
        Write-Warning "$queuepath already exists - no changes were made"
    }
}

Function GetAccountFromWellKnownSid
{
    param(
        [Parameter(Mandatory=$true)]
        [System.Security.Principal.WellKnownSidType] $WellKnownSidType
    )

    $account = New-Object System.Security.Principal.SecurityIdentifier $WellKnownSidType,$null
    return $account.Translate([System.Security.Principal.NTAccount]).ToString()
}

Function ValidateAccount {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $Account
    )

    # Test Account is valid
    $userAccount =  new-object System.Security.Principal.NTAccount($Account)
    try {
        [void] $userAccount.Translate([System.Security.Principal.SecurityIdentifier])
        return $true
    }
    catch [System.Security.Principal.IdentityNotMappedException] {
        Write-Warning "$account does not resolve to a Windows Account"
        return $false
    }
}

Function SetDefaultPermissionsForQueue
{
    param(
        [Parameter(Mandatory=$true)]
        [System.Messaging.MessageQueue] $Queue,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $Account
    )

    $adminGroup = GetAccountFromWellKnownSid -wellKnownSidType ([System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid)
    $Queue.SetPermissions($AdminGroup, "FullControl", "Allow")
    $Queue.SetPermissions($Account, "WriteMessage", "Allow")
    $Queue.SetPermissions($Account, "ReceiveMessage", "Allow")
    $Queue.SetPermissions($Account, "PeekMessage", "Allow")
    $Queue.SetPermissions($Account, "GetQueueProperties", "Allow")
}

# Function to create all the queues for the endpoint.

Function CreateQueuesForEndpoint
{
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $EndpointName,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [ValidateScript({ValidateAccount -Account $_})]
        [string] $Account
    )

    # main queue
    CreateQueue -QueueName $EndpointName -Account $Account

    # timeout queue
    CreateQueue -QueueName "$EndpointName.timeouts" -Account $Account

    # timeout dispatcher queue
    CreateQueue -QueueName "$EndpointName.timeoutsdispatcher" -Account $Account

    # Msmq storage persistence queue. When using endpointConfiguration.UsePersistence<MsmqPersistence, StorageType.Subscriptions>() in your code, uncomment the following queue or replace it with the configured value.
    # CreateQueue -QueueName "$EndpointName.Subscriptions" -Account $Account
}