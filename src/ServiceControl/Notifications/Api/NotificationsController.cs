namespace ServiceControl.Notifications.Api
{
    using System;
    using System.Threading.Tasks;
    using Email;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api")]
    public class NotificationsController(IErrorMessageDataStore store, Settings settings) : ControllerBase
    {
        [Route("notifications/email")]
        [HttpGet]
        public async Task<EmailNotifications> GetEmailNotificationsSettings()
        {
            using var manager = await store.CreateNotificationsManager();
            var settings = await manager.LoadSettings();

            return settings.Email;
        }

        [Route("notifications/email/toggle")]
        [HttpPost]
        public async Task<IActionResult> ToggleEmailNotifications(ToggleEmailNotifications request)
        {
            using var manager = await store.CreateNotificationsManager();
            var settings = await manager.LoadSettings();

            settings.Email.Enabled = request.Enabled;

            await manager.SaveChanges();

            return Ok();
        }

        [Route("notifications/email")]
        [HttpPost]
        public async Task<IActionResult> UpdateSettings(UpdateEmailNotificationsSettingsRequest request)
        {
            using var manager = await store.CreateNotificationsManager();
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

            return Ok();
        }

        [Route("notifications/email/test")]
        [HttpPost]
        public async Task<IActionResult> SendTestEmail()
        {
            using var manager = await store.CreateNotificationsManager();
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
                //TODO compare this to the HttpResponseMessage version that was here
                return Problem($"{e.Message} {e.InnerException?.Message}", title: "Error sending test email notification");
            }

            return Accepted();
        }

        readonly string instanceName = settings.ServiceName;
    }
}
