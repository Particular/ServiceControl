namespace NServiceBus
{
    using Extensibility;
    using Transport.Msmq;

    /// <summary>
    /// Gives users fine grained control over routing via extension methods.
    /// </summary>
    public static class DeadLetterQueueOptionExtensions
    {
        internal const string KeyDeadLetterQueue = "MSMQ.UseDeadLetterQueue";

        /// <summary>
        /// Enable or disable MSMQ dead letter queueing.
        /// </summary>
        /// <param name="options">Option being extended.</param>
        /// <param name="enable">Either enable or disabling message dead letter queueing.</param>
        public static void UseDeadLetterQueue(this ExtendableOptions options, bool enable = true)
        {
            Guard.AgainstNull(nameof(options), options);
            var ext = options.GetExtensions();
            ext.Set(KeyDeadLetterQueue, enable);
        }
    }
}
