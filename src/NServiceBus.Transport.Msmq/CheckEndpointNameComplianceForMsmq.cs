namespace NServiceBus.Transport.Msmq
{
    using System;
    static class CheckEndpointNameComplianceForMsmq
    {
        public static void Check(string endpointName)
        {
            // .NET Messaging API hardcodes the buffer size to 124. As a result, the entire format name of the queue cannot exceed 123
            var formatName = $"DIRECT=OS:{Environment.MachineName}\\private$\\{endpointName}";
            if (formatName.Length > 150)
            {
                throw new InvalidOperationException($"The specified endpoint name {endpointName} is too long. The fully formatted queue name for the endpoint:{formatName} must be 150 characters or less.");
            }
        }
    }
}
