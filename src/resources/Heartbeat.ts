export interface Endpoint {
  id: string;
  name: string;
  host_display_name: string;
  monitor_heartbeat: boolean;
  heartbeat_information?: {
    last_report_at: string;
    reported_status: EndpointStatus;
  };
  is_sending_heartbeats: boolean;
  aliveCount: number;
  downCount: number;
}

export enum EndpointStatus {
  Alive = "beating",
  Dead = "dead",
}
