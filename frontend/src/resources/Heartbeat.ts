export interface LogicalEndpoint {
  name: string;
  monitor_heartbeat: boolean;
  heartbeat_information?: {
    last_report_at: string;
    reported_status: EndpointStatus;
  };
  track_instances: boolean;
  alive_count: number;
  down_count: number;
  muted_count: number;
}

export enum EndpointStatus {
  Alive = "beating",
  Dead = "dead",
}
