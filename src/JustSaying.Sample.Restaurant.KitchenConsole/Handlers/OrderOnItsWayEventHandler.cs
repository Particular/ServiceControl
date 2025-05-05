using System.Security.Cryptography;
using System.Threading.Tasks;
using JustSaying.Messaging;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Sample.Restaurant.Models;
using Microsoft.Extensions.Logging;

namespace JustSaying.Sample.Restaurant.KitchenConsole.Handlers
{
    public class OrderOnItsWayEventHandler : IHandlerAsync<OrderOnItsWayEvent>
    {
        private readonly IMessagePublisher _publisher;
        private readonly ILogger<OrderOnItsWayEventHandler> _logger;

        public OrderOnItsWayEventHandler(IMessagePublisher publisher, ILogger<OrderOnItsWayEventHandler> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        public async Task<bool> Handle(OrderOnItsWayEvent message)
        {
            await Task.Delay(RandomNumberGenerator.GetInt32(50, 100));

            var orderDeliveredEvent = new OrderDeliveredEvent()
            {
                OrderId = message.OrderId
            };

            _logger.LogInformation("Order {OrderId} is on its way!", message.OrderId);

            await _publisher.PublishAsync(orderDeliveredEvent);

            return true;
        }
    }
}
