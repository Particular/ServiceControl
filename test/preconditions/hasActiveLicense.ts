import content from "../mocks/active-license-response.json";
import { SetupFactoryOptions } from "../driver";

export const hasActiveLicense = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}license`, {
    body: content,
  });
  return content;
};
