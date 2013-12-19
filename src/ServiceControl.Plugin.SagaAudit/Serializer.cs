namespace ServiceControl.EndpointPlugin.SagaState
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Messages.SagaState;
    using NServiceBus.MessageInterfaces.MessageMapper.Reflection;
    using NServiceBus.Serializers.Json;

    class Serializer
    {
        static JsonMessageSerializer serializer;

        static Serializer()
        {
            var messageMapper = new MessageMapper();
            messageMapper.Initialize(new[]
                {
                    typeof(SagaUpdatedMessage)
                });
            serializer = new JsonMessageSerializer(messageMapper);
        }

        public static string Serialize(object sagaEntity)
        {
            using (var memoryStream = new MemoryStream())
            {
                serializer.Serialize(new[] {sagaEntity}, memoryStream);
                memoryStream.Position = 0;
                using (var streamReader = new StreamReader(memoryStream))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }
    }
}
