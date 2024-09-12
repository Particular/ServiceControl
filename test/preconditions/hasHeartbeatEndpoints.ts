import { SetupFactoryOptions } from "../driver";
import { EndpointsView } from "@/resources/EndpointView";

export const hasHeartbeatsEndpoints =
  (endpoints: EndpointsView[]) =>
  ({ driver }: SetupFactoryOptions) => {
    driver.mockEndpoint(`${window.defaultConfig.service_control_url}endpoints`, {
      body: endpoints,
    });
    return endpoints;
  };

export const hasNoHeartbeatsEndpoints = hasHeartbeatsEndpoints([]);
