namespace ServiceControl.CustomChecks
{
    using System.Collections.Generic;

    class StatisticsResult
    {
        public IList<CustomCheck> Checks { get; set; }
        public int TotalResults { get; set; }
        public string Etag { get; set; }
    }
}