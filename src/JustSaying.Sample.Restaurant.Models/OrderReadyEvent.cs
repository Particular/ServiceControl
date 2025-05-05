namespace JustSaying.Sample.Restaurant.Models
{
    using JustSaying.Models;

    public class OrderReadyEvent : Message
    {
        public int OrderId { get; set; }
    }
}
