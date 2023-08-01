namespace NServiceBus.Transport.Msmq
{
    using System.IO;
    using System.Xml;
    using System.Xml.Linq;

    class InstanceMappingFileLoader : IInstanceMappingLoader
    {
        string path;

        public InstanceMappingFileLoader(string path)
        {
            this.path = path;
        }

        public XDocument Load()
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = XmlReader.Create(file))
            {
                return XDocument.Load(reader);
            }
        }

        public override string ToString()
        {
            return path;
        }
    }
}