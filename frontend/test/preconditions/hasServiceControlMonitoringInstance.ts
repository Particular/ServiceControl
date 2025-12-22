import { serviceControlMainInstance } from "../mocks/service-control-instance-template";
import { SetupFactoryOptions } from "../driver";
import { getDefaultConfig } from "@/defaultConfig";

export const hasServiceControlMonitoringInstance = ({ driver }: SetupFactoryOptions) => {
  const monitoringInstanceUrl = getDefaultConfig().monitoring_url;
  driver.mockEndpoint(monitoringInstanceUrl, {
    body: serviceControlMainInstance,
    headers: { "X-Particular-Version": "5.0.4" },
  });
  return serviceControlMainInstance;
};
