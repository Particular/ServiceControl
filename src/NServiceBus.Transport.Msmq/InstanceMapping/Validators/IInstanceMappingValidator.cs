namespace NServiceBus.Transport.Msmq
{
    using System.Xml.Linq;

    interface IInstanceMappingValidator
    {
        void Validate(XDocument document);
    }
}