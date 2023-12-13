namespace ServiceControl.Connection
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    public class ConnectionController(IPlatformConnectionBuilder builder) : ControllerBase
    {
        [Route("connection")]
        [HttpGet]
        public async Task<IActionResult> GetConnectionDetails()
        {
            var connectionDetails = await builder.BuildPlatformConnection();

            // TODO previously this was using a default json serializer setting. Why was that needed? Let's verify
            return Ok(new { settings = connectionDetails.ToDictionary(), errors = connectionDetails.Errors });
        }
    }
}
