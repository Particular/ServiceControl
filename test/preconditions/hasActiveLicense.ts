import { activeLicenseResponse } from "../mocks/license-response-template";
import { SetupFactoryOptions } from "../driver";

export const hasActiveLicense = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}license`, {
    body: activeLicenseResponse,
  });
  return activeLicenseResponse;
};
