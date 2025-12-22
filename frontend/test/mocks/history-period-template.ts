import { Endpoint } from "@/resources/MonitoringEndpoint";

export const oneEndpointWithMetricsPoints = (queueLength: number | number[], throughput: number | number[], retries: number | number[], processingTime: number | number[], criticalTime: number | number[]): Endpoint[] => {
  return [
    <Endpoint>{
      name: "Endpoint1",
      isStale: false,
      errorCount: 411,
      serviceControlId: "voluptatibus",
      isScMonitoringDisconnected: false,
      endpointInstanceIds: ["c62841c1e8abe36415eb7ec412cedf58"],
      metrics: {
        queueLength: {
          average: queueLength,
          points: [queueLength],
        },
        throughput: {
          average: throughput,
          points: [throughput],
        },
        retries: {
          average: retries,
          points: [retries],
        },
        processingTime: {
          average: processingTime,
          points: [processingTime],
          timeAxisValues: [],
        },
        criticalTime: {
          average: criticalTime,
          points: [criticalTime],
          timeAxisValues: [],
        },
      },
      disconnectedCount: 0,
      connectedCount: 1,
    },
  ];
};
