namespace ServiceControl.Connection
{
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api")]
    public class ConnectionController(IPlatformConnectionBuilder builder) : ControllerBase
    {
        [Route("connection")]
        [HttpGet]
        public async Task<IActionResult> GetConnectionDetails()
        {
            var platformConnectionDetails = await builder.BuildPlatformConnection();
            // TODO why do these properties need to be lower cased?
            var connectionDetails = new { settings = platformConnectionDetails.ToDictionary(), errors = platformConnectionDetails.Errors };
            // by default snake case is used for serialization so we take care of explicitly serializing here
            var content = JsonSerializer.Serialize(connectionDetails);
            return Content(content, "application/json");
        }
    }
}
