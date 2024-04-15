﻿namespace ServiceControl.Connection
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api")]
    public class ConnectionController(IPlatformConnectionBuilder builder) : ControllerBase
    {
        // This controller doesn't use the default serialization settings because
        // ServicePulse and the Platform Connector Plugin expect the connection
        // details the be serialized and formatted in a specific way
        [Route("connection")]
        [HttpGet]
        public async Task<IActionResult> GetConnectionDetails()
        {
            var platformConnectionDetails = await builder.BuildPlatformConnection();
            return new JsonResult(new ConnectionDetails(platformConnectionDetails.ToDictionary(), platformConnectionDetails.Errors), JsonSerializerOptions.Default);
        }

        // The Settings and Errors properties are serialized as settings and errors
        // because ServicePulse expects them te be lowercase 
        class ConnectionDetails(IDictionary<string, object> settings, ConcurrentBag<string> errors)
        {
            [JsonPropertyName("settings")]
            public IDictionary<string, object> Settings { get; init; } = settings;

            [JsonPropertyName("errors")]
            public ConcurrentBag<string> Errors { get; init; } = errors;
        }
    }
}
