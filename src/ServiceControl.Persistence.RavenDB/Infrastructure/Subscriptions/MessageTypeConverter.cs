﻿namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System;
    using Newtonsoft.Json;
    using NServiceBus.Unicast.Subscriptions;

    class MessageTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(MessageType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return new MessageType(serializer.Deserialize<string>(reader));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value.ToString());
        }
    }
}