namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Xml;
    using System.Xml.Linq;

    class InstanceMappingUriLoader : IInstanceMappingLoader
    {
        Uri path;

        public InstanceMappingUriLoader(Uri path)
        {
            this.path = path;
        }

        public XDocument Load()
        {
            using (var reader = XmlReader.Create(path.ToString()))
            {
                return XDocument.Load(reader);
            }
        }

        public override string ToString()
        {
            return path.ToString();
        }
    }
}