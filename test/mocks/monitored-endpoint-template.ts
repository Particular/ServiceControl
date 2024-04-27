import { Endpoint } from "@/resources/MonitoringEndpoint";

const monitoredEndpointTemplate = <Endpoint>{
  name: "A happy endpoint",
  isStale: false,
  errorCount: 411,
  serviceControlId: "voluptatibus",
  isScMonitoringDisconnected: false,
  endpointInstanceIds: ["c62841c1e8abe36415eb7ec412cedf58"],
  metrics: {
    processingTime: {
      average: 0.0,
      points: [],
      timeAxisValues: [],
    },
    criticalTime: {
      average: 0.0,
      points: [],
      timeAxisValues: [],
    },
    retries: {
      average: 0.0,
      points: [],
    },
    throughput: {
      average: 0.0,
      points: [],
    },
    queueLength: {
      average: 0.0,
      points: [],
    },
  },
  disconnectedCount: 0,
  connectedCount: 1,
};
export default monitoredEndpointTemplate;