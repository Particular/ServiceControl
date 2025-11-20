import { SetupFactoryOptions } from "../driver";
import { getDefaultConfig } from "@/defaultConfig";

const content = JSON.stringify(0);

export const hasNoDisconnectedEndpoints = ({ driver }: SetupFactoryOptions) => {
  const monitoringInstanceUrl = getDefaultConfig().monitoring_url;
  driver.mockEndpoint(`${monitoringInstanceUrl}monitored-endpoints/disconnected`, {
    body: content,
  });
  return content;
};
