using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using JustSaying.Messaging;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Sample.Restaurant.Models;
using Microsoft.Extensions.Logging;

namespace JustSaying.Sample.Restaurant.KitchenConsole.Handlers
{
    public class OrderPlacedEventHandler : IHandlerAsync<OrderPlacedEvent>
    {
        private readonly IMessagePublisher _publisher;
        private readonly ILogger<OrderPlacedEventHandler> _logger;

        /// <summary>
        /// Handles messages of type OrderPlacedEvent
        /// Takes a dependency on IMessagePublisher so that further messages can be published 
        /// </summary>
        public OrderPlacedEventHandler(IMessagePublisher publisher, ILogger<OrderPlacedEventHandler> log)
        {
            _publisher = publisher;
            _logger = log;
        }

        public async Task<bool> Handle(OrderPlacedEvent message)
        {
            // Returning true would indicate:
            //   The message was handled successfully
            //   The message can be removed from the queue.
            // Returning false would indicate:
            //   The message was not handled successfully
            //   The message handling should be retried (configured by default)
            //   The message should be moved to the error queue if all retries fail

            try
            {
                _logger.LogInformation("Order {orderId} for {description} received", message.OrderId, message.Description);

                // This is where you would actually handle the order placement
                // Intentionally left empty for the sake of this being a sample application

                _logger.LogInformation("Order {OrderId} ready", message.OrderId);

                await Task.Delay(RandomNumberGenerator.GetInt32(50, 100));

                var orderReadyEvent = new OrderReadyEvent
                {
                    OrderId = message.OrderId
                };

                await _publisher.PublishAsync(orderReadyEvent).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle message for {orderId}", message.OrderId);
                return false;
            }
        }
    }
}
