import { Endpoint, ExtendedEndpointInstance, EndpointMetrics, ExtendedEndpointDetails, MessageType } from "@/resources/MonitoringEndpoint";

export const monitoredEndpointTemplate = <Endpoint>{
  name: "A happy endpoint",
  isStale: false,
  errorCount: 150,
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

export const monitoredEndpointList: Endpoint[] = [
  <Endpoint>{
    name: "A.C.Test.Shipping",
    isStale: false,
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
        points: [
          0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
          0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
        ],
      },
    },
    disconnectedCount: 0,
    connectedCount: 1,
    errorCount: 0,
    serviceControlId: "",
    isScMonitoringDisconnected: false,
  },
  <Endpoint>{
    name: "A.C.ClientUI",
    isStale: false,
    endpointInstanceIds: ["cce2f6add5189ee34de8af0e2cc9da34"],
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
        points: [
          0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
          0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
        ],
      },
    },
    disconnectedCount: 0,
    connectedCount: 1,
    errorCount: 0,
    serviceControlId: "",
    isScMonitoringDisconnected: false,
  },
  <Endpoint>{
    name: "A.C.Sales1",
    isStale: false,
    endpointInstanceIds: ["6b40b6b994899339d03772d23d5c5f19"],
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
        points: [
          0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
          0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
        ],
      },
    },
    disconnectedCount: 0,
    connectedCount: 1,
    errorCount: 0,
    serviceControlId: "",
    isScMonitoringDisconnected: false,
  },
  <Endpoint>{
    name: "A.C.Billing",
    isStale: false,
    endpointInstanceIds: ["af940336eb7c92f0687af81fe94a0673"],
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
        points: [
          0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
          0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
        ],
      },
    },
    disconnectedCount: 0,
    connectedCount: 1,
    errorCount: 0,
    serviceControlId: "",
    isScMonitoringDisconnected: false,
  },
];

export const monitoredEndpointDetails = <ExtendedEndpointDetails>{
  isScMonitoringDisconnected: false,
  errorCount: 0,
  digest: {
    metrics: {
      processingTime: {
        latest: 0,
        average: 74.82203389830508,
      },
      criticalTime: {
        latest: 0,
        average: 239.78813559322035,
      },
      retries: {
        latest: 0,
        average: 0,
      },
      throughput: {
        latest: 0,
        average: 1.9666666666666666,
      },
      queueLength: {
        latest: 2,
        average: 2,
      },
    },
  },
  instances: [
    {
      id: "d4b8b36ba72b0738feffe71105aaceQ1",
      name: "Endpoint1",
      isStale: false,
      metrics: {
        processingTime: {
          average: 74.82203389830508,
          points: [0],
        },
        criticalTime: {
          average: 239.78813559322035,
          points: [0],
        },
        retries: {
          average: 0,
          points: [0],
        },
        throughput: {
          average: 1.9666666666666666,
          points: [0],
        },
      },
    },
  ],
  messageTypes: [
    {
      id: "Message1, Shared, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
      typeName: "Message1",
      assemblyName: "Shared",
      assemblyVersion: "1.0.0.0",
      culture: "neutral",
      publicKeyToken: "null",
      metrics: {
        processingTime: {
          average: 74.82203389830508,
          points: [0],
        },
        criticalTime: {
          average: 239.78813559322035,
          points: [0],
        },
        retries: {
          average: 0,
          points: [0],
        },
        throughput: {
          average: 1.9666666666666666,
          points: [0],
        },
      },
    },
  ],
  metricDetails: {
    metrics: <EndpointMetrics>{
      processingTime: {
        timeAxisValues: ["2024-06-12T23:47:00Z"],
        average: 74.82203389830508,
        points: [0],
      },
      criticalTime: {
        timeAxisValues: ["2024-06-12T23:47:00Z"],
        average: 239.78813559322035,
        points: [0],
      },
      retries: {
        timeAxisValues: [],
        average: 0,
        points: [0],
      },
      throughput: {
        timeAxisValues: [],
        average: 1.9666666666666666,
        points: [0],
      },
      queueLength: {
        timeAxisValues: [],
        average: 2,
        points: [0],
      },
    },
  },
};

export const messageTypeForEndpoint = <MessageType>{
  id: "Message1, Shared, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
  typeName: "Message1",
  assemblyName: "Shared",
  assemblyVersion: "1.0.0.0",
  culture: "neutral",
  publicKeyToken: "null",
  metrics: {
    processingTime: {
      average: 74.82203389830508,
      points: [0],
    },
    criticalTime: {
      average: 239.78813559322035,
      points: [0],
    },
    retries: {
      average: 0,
      points: [0],
    },
    throughput: {
      average: 1.9666666666666666,
      points: [0],
    },
  },
};

export const instanceForEndpoint = <ExtendedEndpointInstance>{
  id: "d4b8b36ba72b0738feffe71105aaceQ1",
  name: "Endpoint1",
  isStale: false,
  metrics: {
    processingTime: {
      average: 74.82203389830508,
      points: [0],
    },
    criticalTime: {
      average: 239.78813559322035,
      points: [0],
    },
    retries: {
      average: 0,
      points: [0],
    },
    throughput: {
      average: 1.9666666666666666,
      points: [0],
    },
  },
};

export const noMonitoredEndpoints = [];
