namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System;

    
    public class RedirectRequest
    {
        public string fromphysicaladdress { get; set; }
        public string tophysicaladdress { get; set; }
    }
}