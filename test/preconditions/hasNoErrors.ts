import { SetupFactoryOptions } from "../driver";

const content = JSON.stringify([]);

export const errorsDefaultHandler = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}errors`, {
    body: content,
  });
  return content;
};
