namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System;

    public class MessageRedirectFromJson
    {
        public Guid message_redirect_id { get; set; }
        public string from_physical_address { get; set; }
        public string to_physical_address { get; set; }
        public DateTime last_modified { get; set; }
    }
}
