import content from "../mocks/service-pulse-no-platform-update-needed.json";
import { SetupFactoryOptions } from "../driver";

export const hasUpToDateServicePulse = ({ driver }: SetupFactoryOptions) => {
  driver.mockEndpoint(`https://platformupdate.particular.net/servicepulse.txt`, {
    body: content,
  });
  return content;
};
