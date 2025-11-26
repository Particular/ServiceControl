import { EndpointsView } from "@/resources/EndpointView";
import { EndpointStatus } from "@/resources/Heartbeat";

export const healthyEndpointTemplate = <EndpointsView>{
  is_sending_heartbeats: true,
  id: "HealthyEndpoint",
  name: "HealthyEndpoint",
  monitor_heartbeat: true,
  host_display_name: "HealhtyEndpoint.Hostname",
  heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: new Date().toISOString() },
};

export const unHealthyEndpointTemplate = <EndpointsView>{
  is_sending_heartbeats: true,
  id: "UnHealthyEndpoint",
  name: `UnHealthyEndpoint`,
  monitor_heartbeat: true,
  host_display_name: "UnHealhtyEndpoint.Hostname",
  heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: new Date().toISOString() },
};
