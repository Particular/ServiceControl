namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Schema;

    class EmbeddedSchemaInstanceMappingValidator : IInstanceMappingValidator
    {
        public EmbeddedSchemaInstanceMappingValidator(string resourceName)
        {
            using (var stream = GetType().Assembly.GetManifestResourceStream(resourceName))
            using (var xmlReader = XmlReader.Create(stream ?? throw new InvalidOperationException("Could not load resource.")))
            {
                schema = new XmlSchemaSet();
                schema.Add("", xmlReader);
            }
        }

        public void Validate(XDocument document)
        {
            document.Validate(schema, null, false);
        }

        public static IInstanceMappingValidator CreateValidatorV1() => new EmbeddedSchemaInstanceMappingValidator("NServiceBus.Transport.Msmq.InstanceMapping.Validators.endpoints.xsd");
        public static IInstanceMappingValidator CreateValidatorV2() => new EmbeddedSchemaInstanceMappingValidator("NServiceBus.Transport.Msmq.InstanceMapping.Validators.endpointsV2.xsd");

        XmlSchemaSet schema;
    }
}