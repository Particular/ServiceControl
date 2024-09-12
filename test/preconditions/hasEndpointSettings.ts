import { SetupFactoryOptions } from "../driver";
import { EndpointSettings } from "@/resources/EndpointSettings";
import endpointSettingsClient from "@/components/heartbeats/endpointSettingsClient";

export const hasEndpointSettings = function (settings: EndpointSettings[]) {
  if (settings.length === 0) {
    settings.push(endpointSettingsClient.defaultEndpointSettingsValue());
  }
  return ({ driver }: SetupFactoryOptions) => {
    driver.mockEndpoint(`${window.defaultConfig.service_control_url}endpointssettings`, {
      body: settings,
    });
    return settings;
  };
};
