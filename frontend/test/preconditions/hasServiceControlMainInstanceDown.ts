import { serviceControlMainInstance } from "../mocks/service-control-instance-template";
import { SetupFactoryOptions } from "../driver";
import { getDefaultConfig } from "@/defaultConfig";

export const hasServiceControlMainInstanceDown = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = getDefaultConfig().service_control_url;
  driver.mockEndpoint(serviceControlInstanceUrl, {
    body: serviceControlMainInstance,
    headers: { "X-Particular-Version": "5.0.4" },
  });
  return serviceControlMainInstance;
};
