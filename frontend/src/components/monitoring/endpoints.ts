import type { DigestValues, EndpointDigest, EndpointMetrics, EndpointValues, EndpointValuesWithTime, ExtendedEndpointDetails } from "@/resources/MonitoringEndpoint";

const defaultMetricData: EndpointValues = {
  points: [],
  average: 0,
};

const defaultTimeMetricData: EndpointValuesWithTime = {
  ...defaultMetricData,
  timeAxisValues: [],
};

export const emptyEndpointMetrics = (): EndpointMetrics => ({
  queueLength: defaultMetricData,
  throughput: defaultMetricData,
  retries: defaultMetricData,
  processingTime: defaultTimeMetricData,
  criticalTime: defaultTimeMetricData,
});

const defaultDigestValuesData: DigestValues = {};

export const emptyEndpointDigest = (): EndpointDigest => ({
  queueLength: defaultDigestValuesData,
  throughput: defaultDigestValuesData,
  retries: defaultDigestValuesData,
  processingTime: defaultDigestValuesData,
  criticalTime: defaultDigestValuesData,
});

export const emptyEndpointDetails = (): ExtendedEndpointDetails => ({
  instances: [],
  digest: { metrics: emptyEndpointDigest() },
  metricDetails: { metrics: emptyEndpointMetrics() },
  isScMonitoringDisconnected: false,
  serviceControlId: "",
  errorCount: 0,
  isStale: false,
  messageTypes: [],
});
