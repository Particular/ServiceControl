namespace ServiceBus.Management.Infrastructure.Nancy
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using ServiceControl.Infrastructure.SignalR;
    using global::Nancy;
    using global::Nancy.IO;

    public class JsonNetSerializer : ISerializer
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="JsonNetSerializer" /> class.
        /// </summary>
        public JsonNetSerializer()
        {
            var serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new UnderscoreMappingResolver(),
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                Converters = {new IsoDateTimeConverter {DateTimeStyles = DateTimeStyles.RoundtripKind}}
            };
            serializer = JsonSerializer.Create(serializerSettings);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="JsonNetSerializer" /> class,
        ///     with the provided <paramref name="serializer" />.
        /// </summary>
        /// <param name="serializer">Json converters used when serializing.</param>
        public JsonNetSerializer(JsonSerializer serializer)
        {
            this.serializer = serializer;
        }

        /// <summary>
        ///     Whether the serializer can serialize the content type
        /// </summary>
        /// <param name="contentType">Content type to serialise</param>
        /// <returns>True if supported, false otherwise</returns>
        public bool CanSerialize(string contentType)
        {
            return Helpers.IsJsonType(contentType);
        }

        /// <summary>
        ///     Gets the list of extensions that the serializer can handle.
        /// </summary>
        /// <value>An <see cref="IEnumerable{T}" /> of extensions if any are available, otherwise an empty enumerable.</value>
        public IEnumerable<string> Extensions
        {
            get { yield return "json"; }
        }

        /// <summary>
        ///     Serialize the given model with the given contentType
        /// </summary>
        /// <param name="contentType">Content type to serialize into</param>
        /// <param name="model">Model to serialize</param>
        /// <param name="outputStream">Output stream to serialize to</param>
        /// <returns>Serialised object</returns>
        public void Serialize<TModel>(string contentType, TModel model, Stream outputStream)
        {
            using (var writer = new JsonTextWriter(new StreamWriter(new UnclosableStreamWrapper(outputStream))))
            {
                serializer.Serialize(writer, model);
            }
        }

        readonly JsonSerializer serializer;
    }
}