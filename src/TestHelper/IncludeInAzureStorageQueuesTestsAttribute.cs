﻿public class IncludeInAzureStorageQueuesTestsAttribute : IncludeInTestsAttribute
{
    protected override string Filter => "Transports.AzureStorageQueues";
}
