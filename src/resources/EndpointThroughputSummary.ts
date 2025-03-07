interface EndpointThroughputSummary {
  name: string;
  is_known_endpoint: boolean;
  user_indicator: string;
  max_daily_throughput: number;
  max_monthly_throughput?: number;
}

export default EndpointThroughputSummary;
