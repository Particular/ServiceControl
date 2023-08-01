namespace NServiceBus
{
    using Extensibility;
    using Transport.Msmq;

    /// <summary>
    /// Gives users fine grained control over routing via extension methods.
    /// </summary>
    public static class JournalOptionExtensions
    {
        internal const string KeyJournaling = "MSMQ.UseJournalQueue";

        /// <summary>
        /// Enable or disable MSMQ journaling.
        /// </summary>
        /// <param name="options">Option being extended.</param>
        /// <param name="enable">Either enable or disabling message journaling.</param>
        public static void UseJournalQueue(this ExtendableOptions options, bool enable = true)
        {
            Guard.AgainstNull(nameof(options), options);
            var ext = options.GetExtensions();
            ext.Set(KeyJournaling, enable);
        }
    }
}
