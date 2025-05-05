using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using JustSaying.Messaging;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Sample.Restaurant.Models;
using Microsoft.Extensions.Logging;

namespace JustSaying.Sample.Restaurant.OrderingApi.Handlers
{
    public class OrderReadyEventHandler : IHandlerAsync<OrderReadyEvent>
    {
        private readonly ILogger<OrderReadyEventHandler> _logger;
        private readonly IMessagePublisher _publisher;

        public OrderReadyEventHandler(ILogger<OrderReadyEventHandler> logger, IMessagePublisher publisher)
        {
            _logger = logger;
            _publisher = publisher;
        }

        public async Task<bool> Handle(OrderReadyEvent message)
        {
            _logger.LogInformation("Order {orderId} ready", message.OrderId);

            // This is where you would actually handle the order placement
            // Intentionally left empty for the sake of this being a sample application

            await Task.Delay(RandomNumberGenerator.GetInt32(50, 100));

            var orderOnItsWayEvent = new OrderOnItsWayEvent()
            {
                OrderId = message.OrderId
            };

            await _publisher.PublishAsync(orderOnItsWayEvent);

            // Returning true would indicate:
            //   The message was handled successfully
            //   The message can be removed from the queue.
            // Returning false would indicate:
            //   The message was not handled successfully
            //   The message handling should be retried (configured by default)
            //   The message should be moved to the error queue if all retries fail
            return true;
        }
    }
}
