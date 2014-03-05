namespace ServiceControl.Plugin.SagaAudit
{
    using System.IO;
    using NServiceBus.Serializers.Binary;
    using NServiceBus.Serializers.Json;

    class Serializer
    {
        static JsonMessageSerializer serializer;

        static Serializer()
        {
            serializer = new JsonMessageSerializer(new SimpleMessageMapper());
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
