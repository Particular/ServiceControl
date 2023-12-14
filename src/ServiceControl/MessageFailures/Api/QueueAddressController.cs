namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;
    using ServiceControl.Persistence;

    [ApiController]
    public class QueueAddressController(IQueueAddressStore store) : ControllerBase
    {
        [Route("errors/queues/addresses")]
        [HttpGet]
        public async Task<IList<QueueAddress>> GetAddresses([FromQuery] PagingInfo pagingInfo)
        {
            var result = await store.GetAddresses(pagingInfo);

            Response.WithQueryResults(result.QueryStats, pagingInfo);

            return result.Results;
        }

        [Route("errors/queues/addresses/search/{search}")]
        [HttpGet]
        public async Task<ActionResult<IList<QueueAddress>>> GetAddressesBySearchTerm([FromQuery] PagingInfo pagingInfo, string search = null)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return BadRequest();
            }

            var result = await store.GetAddressesBySearchTerm(search, pagingInfo);

            Response.WithQueryResults(result.QueryStats, pagingInfo);

            return Ok(result.Results);
        }
    }
}