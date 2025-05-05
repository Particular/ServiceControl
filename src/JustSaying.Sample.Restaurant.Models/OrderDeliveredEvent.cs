namespace JustSaying.Sample.Restaurant.Models
{
    using JustSaying.Models;

    public class OrderDeliveredEvent : Message
    {
        public int OrderId { get; set; }
    }
}
