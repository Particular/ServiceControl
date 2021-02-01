namespace ServiceControl.AcceptanceTests.Recoverability.MessageRedirects
{
    using System;

    public class MessageRedirectFromJson
    {
#pragma warning disable IDE1006 // Naming Styles
        public Guid message_redirect_id { get; set; }
        public string from_physical_address { get; set; }
        public string to_physical_address { get; set; }
        public DateTime last_modified { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }
}