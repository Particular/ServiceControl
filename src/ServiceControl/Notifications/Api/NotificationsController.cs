namespace ServiceControl.Notifications.Api
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Email;
    using Infrastructure.Auth;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api")]
    public class NotificationsController(INotificationsDataStore store, Settings settings, EmailSender emailSender) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorNotificationsView)]
        [Route("notifications/email")]
        [HttpGet]
        public async Task<EmailNotifications> GetEmailNotificationsSettings(CancellationToken cancellationToken = default)
        {
            var notificationsSettings = await store.LoadSettings(cancellationToken);

            return notificationsSettings.Email;
        }

        [Authorize(Policy = Permissions.ErrorNotificationsManage)]
        [Route("notifications/email/toggle")]
        [HttpPost]
        public async Task<IActionResult> ToggleEmailNotifications(ToggleEmailNotifications request, CancellationToken cancellationToken = default)
        {
            var notificationsSettings = await store.LoadSettings(cancellationToken);

            notificationsSettings.Email.Enabled = request.Enabled;

            await store.SaveSettings(notificationsSettings, cancellationToken);

            return Ok();
        }

        [Authorize(Policy = Permissions.ErrorNotificationsManage)]
        [Route("notifications/email")]
        [HttpPost]
        public async Task<IActionResult> UpdateSettings(UpdateEmailNotificationsSettingsRequest request, CancellationToken cancellationToken = default)
        {
            var notificationsSettings = await store.LoadSettings(cancellationToken);

            var emailSettings = notificationsSettings.Email;

            emailSettings.SmtpServer = request.SmtpServer;
            emailSettings.SmtpPort = request.SmtpPort;

            emailSettings.AuthenticationAccount = request.AuthorizationAccount;
            emailSettings.AuthenticationPassword = request.AuthorizationPassword;
            emailSettings.EnableTLS = request.EnableTLS;

            emailSettings.From = request.From;
            emailSettings.To = request.To;

            await store.SaveSettings(notificationsSettings, cancellationToken);

            return Ok();
        }

        [Authorize(Policy = Permissions.ErrorNotificationsTest)]
        [Route("notifications/email/test")]
        [HttpPost]
        public async Task<IActionResult> SendTestEmail(CancellationToken cancellationToken = default)
        {
            var notificationsSettings = await store.LoadSettings(cancellationToken);

            try
            {
                await emailSender.Send(
                        notificationsSettings.Email,
                        $"[{settings.InstanceName}] health check notification check successful",
                        $"[{settings.InstanceName}] health check notification check successful.",
                        cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
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
