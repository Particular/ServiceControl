namespace ServiceControl.CustomChecks
{
    using System;
    using Contracts.Operations;

    class EndpointDetailsMapper
    {
        public EndpointDetails Parse(object value)
        {
            var stringValue = (string)value;
            var parts = stringValue.Split(';');
            return new EndpointDetails
            {
                HostId = Guid.Parse(parts[0]),
                Host = parts[1],
                Name = parts[2]
            };
        }

        public string Serialize(EndpointDetails value)
        {
            return string.Join(";", value.HostId.ToString(), value.Host, value.Name);
        }
    }
}