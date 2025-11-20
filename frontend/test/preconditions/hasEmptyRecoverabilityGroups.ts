import { SetupFactoryOptions } from "../driver";
import { getDefaultConfig } from "@/defaultConfig";

export const hasRecoverabilityGroups = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = getDefaultConfig().service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}recoverability/groups/Endpoint%20Name`, {
    body: [],
  });
  return [];
};
