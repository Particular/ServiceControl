import { serviceControlMainInstance } from "../mocks/service-control-instance-template";
import { SetupFactoryOptions } from "../driver";

export const hasServiceControlMainInstance =
  (serviceControlVersion = "5.0.4") =>
  ({ driver }: SetupFactoryOptions) => {
    const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
    driver.mockEndpoint(serviceControlInstanceUrl, {
      body: serviceControlMainInstance,
      headers: { "X-Particular-Version": serviceControlVersion },
    });
    return serviceControlMainInstance;
  };
