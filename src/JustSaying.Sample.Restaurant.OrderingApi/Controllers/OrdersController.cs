using System;
using System.Threading.Tasks;
using JustSaying.Messaging;
using JustSaying.Sample.Restaurant.Models;
using JustSaying.Sample.Restaurant.OrderingApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JustSaying.Sample.Restaurant.OrderingApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IMessagePublisher _publisher;
        private readonly ILogger<OrdersController> _log;

        public OrdersController(IMessagePublisher publisher, ILogger<OrdersController> log)
        {
            _publisher = publisher;
            _log = log;
        }

        // POST api/orders
        [HttpPost]
        public async Task PostAsync([FromBody] CustomerOrderModel order)
        {
            _log.LogInformation("Order received for {description}", order.Description);

            // Save order to database generating OrderId
            var orderId = new Random().Next(1, 100);

            var message = new OrderPlacedEvent
            {
                OrderId = orderId,
                Description = order.Description
            };

            await _publisher.PublishAsync(message);

            _log.LogInformation("Order {orderId} placed", orderId);
        }
    }
}
