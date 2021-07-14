namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;

    /// <summary>
    /// A message used by ServiceControl before Audit split (before 4.0) to inform that the audited message has been successfully retried. When upgrading from 3.x to 4.20 and higher
    /// these legacy messages may still be in the input queue so a handler for these is needed.
    /// </summary>
    public partial class MessageFailureResolvedByRetry : IMessage
    {
    }
}