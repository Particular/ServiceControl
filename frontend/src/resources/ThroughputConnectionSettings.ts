export default interface ThroughputConnectionSettings {
  service_control_settings: ThroughputConnectionSetting[];
  monitoring_settings: ThroughputConnectionSetting[];
  broker_settings: ThroughputConnectionSetting[];
}

export interface ThroughputConnectionSetting {
  name: string;
  description: string;
}
