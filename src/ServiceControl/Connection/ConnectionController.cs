namespace ServiceControl.Connection
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
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
            var connectionDetails = new ConnectionDetails(platformConnectionDetails.ToDictionary(), platformConnectionDetails.Errors);
            // by default snake case is used for serialization so we take care of explicitly serializing here
            var content = JsonSerializer.Serialize(connectionDetails);
            return Content(content, "application/json");
        }

        public record ConnectionDetails(IDictionary<string, object> Settings, ConcurrentBag<string> Errors);
    }
}
