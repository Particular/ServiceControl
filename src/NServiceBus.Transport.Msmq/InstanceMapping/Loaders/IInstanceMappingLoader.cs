namespace NServiceBus.Transport.Msmq
{
    using System.Xml.Linq;

    interface IInstanceMappingLoader
    {
        XDocument Load();
    }
}