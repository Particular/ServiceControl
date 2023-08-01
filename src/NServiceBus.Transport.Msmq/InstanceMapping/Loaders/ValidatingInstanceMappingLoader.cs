namespace NServiceBus.Transport.Msmq
{
    using System.Xml.Linq;

    class ValidatingInstanceMappingLoader : IInstanceMappingLoader
    {
        public ValidatingInstanceMappingLoader(IInstanceMappingLoader loader, IInstanceMappingValidator validator)
        {
            this.loader = loader;
            this.validator = validator;
        }

        public XDocument Load()
        {
            var doc = loader.Load();
            validator.Validate(doc);
            return doc;
        }

        public override string ToString() => loader.ToString();

        IInstanceMappingLoader loader;
        IInstanceMappingValidator validator;
    }
}