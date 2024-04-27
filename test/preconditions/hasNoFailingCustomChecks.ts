import { SetupFactoryOptions } from "../driver";

const content = JSON.stringify([]);

export const hasNoFailingCustomChecks = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}customchecks`, {
    body: content,
  });
  return content;
};
