export default interface MonitoredEndpoint {
  Name: string;
  IsStale: boolean;
  EndpointInstanceIds: string[];
  Metrics: { [key: string]: MonitoredValues };
  DisconnectedCount: number;
  ConnectedCount: number;
}

export interface MonitoredValues {
  Average?: number;
  Points: number[];
}
