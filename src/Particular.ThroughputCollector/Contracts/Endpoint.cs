//namespace Particular.ThroughputCollector.Contracts
//{
//#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
//    public class Endpoint
//    {
//        public Endpoint(string name, string queue, ThroughputSource throughputSource)
//        {
//            Name = name;
//            Queue = queue;
//            ThroughputSource = throughputSource;
//            DailyThroughput = [];
//        }

//        public string Name { get; set; }
//        public string Queue { get; set; }
//        public ThroughputSource ThroughputSource { get; set; }
//        public string Scope { get; set; } //used for SQLServer endpoints, perhaps for others too
//        public string[] EndpointIndicators { get; set; }
//        public bool UserIndicatedSendOnly { get; set; }
//        public bool UserIndicatedToIgnore { get; set; }
//        public List<EndpointThroughput> DailyThroughput { get; set; }
//    }

//    public class EndpointThroughput
//    {
//        public DateTime DateUTC { get; set; }
//        public int TotalThroughput { get; set; }
//    }

//    public enum ThroughputSource
//    {
//        Broker,
//        Monitoring,
//        Audit
//    }
//}
