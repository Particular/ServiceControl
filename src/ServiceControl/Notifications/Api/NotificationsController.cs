namespace ServiceControl.Notifications.Api
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Email;
    using Infrastructure.WebApi;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    class NotificationsController : ApiController
    {
        public NotificationsController(IErrorMessageDataStore store, Settings settings)
        {
            this.store = store;
            instanceName = settings.ServiceName;
        }

        [Route("notifications/email")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetEmailNotificationsSettings(HttpRequestMessage request)
        {
            using (var manager = await store.CreateNotificationsManager())
            {
                var settings = await manager.LoadSettings();

                return Negotiator.FromModel(request, settings.Email);
            }
        }

        [Route("notifications/email/toggle")]
        [HttpPost]
        public async Task<HttpResponseMessage> ToggleEmailNotifications(ToggleEmailNotifications request)
        {
            using (var manager = await store.CreateNotificationsManager())
            {
                var settings = await manager.LoadSettings();

                settings.Email.Enabled = request.Enabled;

                await manager.SaveChanges();
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [Route("notifications/email")]
        [HttpPost]
        public async Task<HttpResponseMessage> UpdateSettings(UpdateEmailNotificationsSettingsRequest request)
        {
            using (var manager = await store.CreateNotificationsManager())
            {
                var settings = await manager.LoadSettings();

                var emailSettings = settings.Email;

                emailSettings.SmtpServer = request.SmtpServer;
                emailSettings.SmtpPort = request.SmtpPort;

                emailSettings.AuthenticationAccount = request.AuthorizationAccount;
                emailSettings.AuthenticationPassword = request.AuthorizationPassword;
                emailSettings.EnableTLS = request.EnableTLS;

                emailSettings.From = request.From;
                emailSettings.To = request.To;

                await manager.SaveChanges();

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        [Route("notifications/email/test")]
        [HttpPost]
        public async Task<HttpResponseMessage> SendTestEmail()
        {
            using (var manager = await store.CreateNotificationsManager())
            {
                var settings = await manager.LoadSettings();

                try
                {
                    await EmailSender.Send(
                            settings.Email,
                            $"[{instanceName}] health check notification check successful",
                            $"[{instanceName}] health check notification check successful.");
                }
                catch (Exception e)
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent($"{e.Message} {e.InnerException?.Message}"),
                        ReasonPhrase = "Error sending test email notification"
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }
        }

        readonly IErrorMessageDataStore store;
        readonly string instanceName;
    }
}
