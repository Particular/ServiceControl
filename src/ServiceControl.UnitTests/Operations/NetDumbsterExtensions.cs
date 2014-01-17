namespace ServiceControl.UnitTests.Operations
{
    using System.Linq;
    using netDumbster.smtp;

    public static class NetDumbsterExtensions
    {
        public static string Body(this SmtpMessage smtpMessage)
        {
            return smtpMessage.MessageParts.First().BodyData;
        }
    }
}