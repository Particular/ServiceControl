export interface MonitoringResource {
  name: string;
  label: string;
  unit?: string;
  tooltip?: string;
}

export const MessageType: MonitoringResource = {
  name: "message-type-name",
  label: "Message type name",
};

export const InstanceName: MonitoringResource = {
  name: "instanceName",
  label: "Instance Name",
};

export const EndpointName: MonitoringResource = {
  name: "name",
  label: "Endpoint Name",
};

export const Throughput: MonitoringResource = {
  name: "throughput",
  label: "Throughput",
  unit: "(msgs/s)",
  tooltip: "Throughput: The number of messages per second successfully processed by a receiving endpoint.",
};

export const ScheduledRetries: MonitoringResource = {
  name: "retries",
  label: "Scheduled retries",
  unit: "(msgs/s)",
  tooltip: "Scheduled retries: The number of messages per second scheduled for retries (immediate or delayed).",
};

export const ProcessingTime: MonitoringResource = {
  name: "processingTime",
  label: "Processing time",
  unit: "(t)",
  tooltip: "Processing time: The time taken for a receiving endpoint to successfully process a message.",
};

export const CriticalTime: MonitoringResource = {
  name: "criticalTime",
  label: "Critical time",
  unit: "(t)",
  tooltip: "Critical time: The elapsed time from when a message was sent, until it was successfully processed by a receiving endpoint.",
};

export const QueueLength: MonitoringResource = {
  name: "queueLength",
  label: "Queue length",
  unit: "(msgs)",
  tooltip: "Queue length: The number of messages waiting to be processed in the input queue(s) of the endpoint.",
};
