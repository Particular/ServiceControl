import { serviceControlMainInstance } from "../mocks/service-control-instance-template";
import { SetupFactoryOptions } from "../driver";

export const hasServiceControlMainInstance = ({ driver }: SetupFactoryOptions, serviceControlVersion = "5.0.4") => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(serviceControlInstanceUrl, {
    body: serviceControlMainInstance,
    headers: { "X-Particular-Version": serviceControlVersion },
  });
  return serviceControlMainInstance;
};
