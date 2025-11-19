import { SetupFactoryOptions } from "../driver";

export const hasRecoverabilityGroups = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}recoverability/groups/Endpoint%20Name`, {
    body: [],
  });
  return [];
};
