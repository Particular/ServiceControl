import { monitoredInstanceTemplate } from "../mocks/service-control-instance-template";
import { SetupFactoryOptions } from "../driver";

export const hasServiceControlMonitoringInstanceUrl =
  (url: string) =>
  ({ driver }: SetupFactoryOptions) => {
    const content = monitoredInstanceTemplate;
    window.defaultConfig.monitoring_urls[0] = url;
    driver.mockEndpoint(url, {
      body: content,
      headers: { "X-Particular-Version": "5.0.4" },
    });
    return content;
  };
