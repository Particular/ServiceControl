import { SetupFactoryOptions } from "../driver";
import { EndpointSettings } from "@/resources/EndpointSettings";

export const hasEndpointSettings = function (settings: EndpointSettings[]) {
  if (settings.length === 0) {
    settings.push({ name: "", track_instances: true });
  }
  return ({ driver }: SetupFactoryOptions) => {
    driver.mockEndpoint(`${window.defaultConfig.service_control_url}endpointssettings`, {
      body: settings,
    });
    return settings;
  };
};
