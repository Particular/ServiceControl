namespace JustSaying.Sample.Restaurant.Models
{
    using JustSaying.Models;

    public class OrderPlacedEvent : Message
    {
        public int OrderId { get; set; }

        public string Description { get; set; }
    }
}
