import content from "../mocks/service-control-monitoring-instance.json";
import { SetupFactoryOptions } from "../driver";

export const hasServiceControlMonitoringInstance = ({ driver }: SetupFactoryOptions) => {
  const monitoringInstanceUrl = window.defaultConfig.monitoring_urls[0];
  driver.mockEndpoint(monitoringInstanceUrl, {
    body: content,
    headers: { "X-Particular-Version": "5.0.4" },
  });
  return content;
};
