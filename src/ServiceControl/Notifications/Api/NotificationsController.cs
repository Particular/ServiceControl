namespace ServiceControl.Notifications.Api
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Email;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api")]
    public class NotificationsController(IErrorMessageDataStore store, Settings settings, EmailSender emailSender) : ControllerBase
    {
        [Route("notifications/email")]
        [HttpGet]
        public async Task<EmailNotifications> GetEmailNotificationsSettings()
        {
            using var manager = await store.CreateNotificationsManager();
            var notificationsSettings = await manager.LoadSettings();

            return notificationsSettings.Email;
        }

        [Route("notifications/email/toggle")]
        [HttpPost]
        public async Task<IActionResult> ToggleEmailNotifications(ToggleEmailNotifications request)
        {
            using var manager = await store.CreateNotificationsManager();
            var notificationsSettings = await manager.LoadSettings();

            notificationsSettings.Email.Enabled = request.Enabled;

            await manager.SaveChanges();

            return Ok();
        }

        [Route("notifications/email")]
        [HttpPost]
        public async Task<IActionResult> UpdateSettings(UpdateEmailNotificationsSettingsRequest request)
        {
            using var manager = await store.CreateNotificationsManager();
            var notificationsSettings = await manager.LoadSettings();

            var emailSettings = notificationsSettings.Email;

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
            var notificationsSettings = await manager.LoadSettings();

            try
            {
                await emailSender.Send(
                        notificationsSettings.Email,
                        $"[{settings.ServiceControl.InstanceName}] health check notification check successful",
                        $"[{settings.ServiceControl.InstanceName}] health check notification check successful.");
            }
            catch (Exception e)
            {
                // This is currently done in awkward ways to not having to introduce problem details etc to SP just yet.
                Response.Headers["X-Particular-Reason"] = "Error sending test email notification";
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Content($"{e.Message} {e.InnerException?.Message}");
            }

            return Accepted();
        }
    }
}
