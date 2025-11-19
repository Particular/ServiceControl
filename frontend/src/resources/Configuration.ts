export default interface Configuration {
  host: Host;
  data_retention: DataRetention;
  performance_tunning: PerformanceTuning;
  transport: Transport;
  plugins: Plugins;
  mass_transit_connector?: MassTransitConnector;
}
interface MassTransitConnector {
  version: string;
  logs: Array<{ level: string; message: string; date: string }>;
  error_queues: Array<{ name: string; ingesting: boolean }>;
}
interface Plugins {
  heartbeat_grace_period: string;
}
interface Transport {
  transport_type: string;
  error_log_queue: string;
  error_queue: string;
  forward_error_messages: boolean;
}
interface PerformanceTuning {
  http_default_connection_limit: number;
  external_integrations_dispatching_batch_size: number;
  expiration_process_batch_size: number;
  expiration_process_timer_in_seconds: number;
}
interface DataRetention {
  error_retention_period: string;
}
interface Host {
  service_name: string;
  raven_db_path: string;
  logging: Logging;
}
interface Logging {
  log_path: string;
  logging_level: string;
  raven_db_log_level: string;
}

export interface EditAndRetryConfig {
  enabled: boolean;
  sensitive_headers: string[];
  locked_headers: string[];
}
