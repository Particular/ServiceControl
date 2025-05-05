namespace JustSaying.Sample.Restaurant.Models
{
    using JustSaying.Models;

    public class OrderOnItsWayEvent : Message
    {
        public int OrderId { get; set; }
    }
}
