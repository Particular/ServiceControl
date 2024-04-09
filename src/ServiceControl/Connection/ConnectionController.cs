namespace ServiceControl.Connection
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using JsonSerializer = System.Text.Json.JsonSerializer;

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
            var content = JsonSerializer.Serialize(connectionDetails);
            return Content(content, "application/json");
        }

        // Backward compatibility reason:
        // to make it so that the latest ServicePulse can talk to ServiceControl 5.0.5
        // the Errors and Settings properties must be serialized camelCase 
        class ConnectionDetails(IDictionary<string, object> settings, ConcurrentBag<string> errors)
        {
            [JsonPropertyName("settings")]
            public IDictionary<string, object> Settings { get; init; } = settings;

            [JsonPropertyName("errors")]
            public ConcurrentBag<string> Errors { get; init; } = errors;
        }
    }
}
