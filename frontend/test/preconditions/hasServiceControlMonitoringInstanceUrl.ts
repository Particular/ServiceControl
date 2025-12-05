import { monitoredInstanceTemplate } from "../mocks/service-control-instance-template";
import { SetupFactoryOptions } from "../driver";
import { getDefaultConfig, setDefaultConfig } from "@/defaultConfig";

export const hasServiceControlMonitoringInstanceUrl =
  (url: string) =>
  ({ driver }: SetupFactoryOptions) => {
    const content = monitoredInstanceTemplate;
    const config = getDefaultConfig();
    setDefaultConfig({ ...config, monitoring_url: url });
    driver.mockEndpoint(url, {
      body: content,
      headers: { "X-Particular-Version": "5.0.4" },
    });
    return content;
  };
