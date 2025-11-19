import * as historyPeriodTemplate from "../mocks/history-period-template";
import { SetupFactoryOptions } from "../driver";

export const hasEndpointWithMetricsPoints =
  (queueLength: number | number[], throughput: number | number[], retries: number | number[], processingTime: number | number[], criticalTime: number | number[]) =>
  ({ driver }: SetupFactoryOptions) => {
    const body = historyPeriodTemplate.oneEndpointWithMetricsPoints(queueLength, throughput, retries, processingTime, criticalTime);
    driver.mockEndpoint(`${window.defaultConfig.monitoring_urls[0]}monitored-endpoints`, {
      body: body,
    });
    return [body];
  };
