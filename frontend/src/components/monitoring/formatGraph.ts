import { type EndpointValues } from "@/resources/MonitoringEndpoint";
import { useFormatTime, useFormatLargeNumber, type ValueWithUnit } from "../../composables/formatter";

export function formatGraphDuration(input?: EndpointValues): ValueWithUnit {
  if (input != null) {
    const lastValue = input.points.length > 0 ? input.points[input.points.length - 1] : 0;
    return useFormatTime(lastValue);
  }
  return { value: "0", unit: "" };
}

export function formatGraphDecimalFromNumber(input?: number, deci?: number): string {
  input = input ?? 0;
  let decimals = 0;
  if (input < 10 || input > 1000000) {
    decimals = 2;
  }
  return useFormatLargeNumber(input, deci || decimals);
}

export function formatGraphDecimal(input?: EndpointValues, deci?: number): string {
  input = input ?? {
    points: [],
    average: 0,
  };
  const lastValue = input.points.length > 0 ? input.points[input.points.length - 1] : 0;
  return formatGraphDecimalFromNumber(lastValue, deci);
}

export const largeGraphsMinimumYAxis = Object.freeze({
  queueLength: 10,
  throughputRetries: 10,
  processingCritical: 10,
});

export const smallGraphsMinimumYAxis = Object.freeze({
  queueLength: 10,
  throughput: 10,
  retries: 10,
  processingTime: 10,
  criticalTime: 10,
});
