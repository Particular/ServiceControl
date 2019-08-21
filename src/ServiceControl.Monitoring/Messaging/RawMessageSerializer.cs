namespace ServiceControl.Monitoring.Messaging
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public abstract class RawMessageSerializer<T> where T : RawMessage, new()
    {
        static readonly object[] NoMessages = new object[0];

        public void Serialize(object message, Stream stream)
        {
            throw new NotImplementedException();
        }

        protected abstract bool Store(long timestamp, BinaryReader reader, T message);

        protected object[] DeserializeRawMessage(Stream stream)
        {
            var reader = new BinaryReader(stream);

            var version = reader.ReadInt64();

            if (version == 1)
            {
                var baseTicks = reader.ReadInt64();
                var count = reader.ReadInt32();

                if (count == 0)
                {
                    return NoMessages;
                }

                var messages = new List<object>(1); // usual case

                T message = null;

                for (var i = 0; i < count; i++)
                {
                    if (i % RawMessage.MaxEntries == 0)
                    {
                        message = RawMessage.Pool<T>.Default.Lease();
                        messages.Add(message);
                    }

                    var ticks = reader.ReadInt32();
                    var timestamp = baseTicks + ticks;

                    if (Store(timestamp, reader, message) == false)
                    {
                        throw new Exception("The value should have been written to a newly leased message");
                    }
                }

                return messages.ToArray();
            }

            throw new Exception($"The message version number '{version}' cannot be handled properly.");
        }
    }
}