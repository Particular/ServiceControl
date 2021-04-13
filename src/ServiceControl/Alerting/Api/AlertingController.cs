namespace ServiceControl.Alerting.Api
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.WebApi;

    public class AlertingController : ApiController
    {

        [Route("alerting")]
        [HttpGet]
        public Task<HttpResponseMessage> GetAlertingSettings()
        {
            var settings = new
            {
                SmtpAddress = "smtp.test.net",
                SmtpPort = 1234,
                AuthorizationAccount = "account",
                //TODO: check if the password is set and convert to a flag
                AuthroizationPassword = "",
                EnableSSL = false,
                AlertingEnabled = true
            };

            //TODO: add etag
            var result = Negotiator.FromModel(Request, settings);

            return Task.FromResult(result);
        }
    }
}
