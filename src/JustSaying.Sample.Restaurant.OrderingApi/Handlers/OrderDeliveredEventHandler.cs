using System.Security.Cryptography;
using System.Threading.Tasks;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Sample.Restaurant.Models;
using Microsoft.Extensions.Logging;

namespace JustSaying.Sample.Restaurant.OrderingApi.Handlers
{
    public class OrderDeliveredEventHandler : IHandlerAsync<OrderDeliveredEvent>
    {
        private readonly ILogger<OrderDeliveredEventHandler> _logger;

        public OrderDeliveredEventHandler(ILogger<OrderDeliveredEventHandler> logger)
        {
            _logger = logger;
        }

        public async Task<bool> Handle(OrderDeliveredEvent message)
        {
            await Task.Delay(RandomNumberGenerator.GetInt32(50, 100));

            _logger.LogInformation("Order {OrderId} has been delivered", message.OrderId);
            return true;
        }
    }
}
