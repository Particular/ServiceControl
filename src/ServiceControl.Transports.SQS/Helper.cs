namespace ServiceControl.Transports.SQS
{
    using System;
    using System.Reflection;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Unicast.Messages;

    static class Helper
    {
        public static void ApplyHacksForNsbRaw(this TransportExtensions<SqsTransport> extensions)
        {
            var settings = extensions.GetSettings();

            var conventions = settings.Get<Conventions>();

            bool isMessageType(Type t)
            {
                return conventions.IsMessageType(t);
            }

            var ctor = typeof(MessageMetadataRegistry).GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Func<Type, bool>) }, null);
            settings.Set((MessageMetadataRegistry)ctor.Invoke(new object[] { (Func<Type, bool>)isMessageType }));
        }
    }
}