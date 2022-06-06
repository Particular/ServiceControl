﻿namespace ServiceControl.Notifications.Api
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;
    using Email;
    using Infrastructure.SignalR;
    using Newtonsoft.Json;
    using Raven.Client;

    public class NotificationsController : ApiController
    {
        public NotificationsController(IDocumentStore store, NotificationsAppSettings settings)
        {
            this.store = store;
            instanceName = settings.InstanceName;
        }

        [Route("notifications/email")]
        [HttpGet]
        public async Task<JsonResult<EmailNotifications>> GetEmailNotificationsSettings(HttpRequestMessage request)
        {
            using (var session = store.OpenAsyncSession())
            {
                var settings = await LoadSettings(session).ConfigureAwait(false);

                return new JsonResult<EmailNotifications>(
                    settings.Email,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Include,
                        ContractResolver = new UnderscoreMappingResolver()
                    },
                    Encoding.Unicode,
                    request);
            }
        }

        [Route("notifications/email/toggle")]
        [HttpPost]
        public async Task<HttpResponseMessage> ToggleEmailNotifications(ToggleEmailNotifications request)
        {
            using (var session = store.OpenAsyncSession())
            {
                var settings = await LoadSettings(session).ConfigureAwait(false);

                settings.Email.Enabled = request.Enabled;

                await session.SaveChangesAsync().ConfigureAwait(false);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [Route("notifications/email")]
        [HttpPost]
        public async Task<HttpResponseMessage> UpdateSettings(UpdateEmailNotificationsSettingsRequest request)
        {
            using (var session = store.OpenAsyncSession())
            {
                var settings = await LoadSettings(session).ConfigureAwait(false);

                var emailSettings = settings.Email;

                emailSettings.SmtpServer = request.SmtpServer;
                emailSettings.SmtpPort = request.SmtpPort;

                emailSettings.AuthenticationAccount = request.AuthorizationAccount;
                emailSettings.AuthenticationPassword = request.AuthorizationPassword;
                emailSettings.EnableTLS = request.EnableTLS;

                emailSettings.From = request.From;
                emailSettings.To = request.To;

                await session.SaveChangesAsync().ConfigureAwait(false);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        [Route("notifications/email/test")]
        [HttpPost]
        public async Task<HttpResponseMessage> SendTestEmail()
        {
            using (var session = store.OpenAsyncSession())
            {
                var settings = await LoadSettings(session).ConfigureAwait(false);

                try
                {
                    await EmailSender.Send(
                            settings.Email,
                            $"[{instanceName}] health check notification check successful",
                            $"[{instanceName}] health check notification check successful.")
                        .ConfigureAwait(false);
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

        static async Task<NotificationsSettings> LoadSettings(IAsyncDocumentSession session)
        {
            var settings = await session.LoadAsync<NotificationsSettings>(NotificationsSettings.SingleDocumentId).ConfigureAwait(false);

            if (settings == null)
            {
                settings = new NotificationsSettings
                {
                    Id = NotificationsSettings.SingleDocumentId
                };

                await session.StoreAsync(settings).ConfigureAwait(false);
            }

            return settings;
        }

        IDocumentStore store;
        string instanceName;
    }
}
