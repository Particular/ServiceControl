import { serviceControlNoPlatformUpdatesNeeded } from "../mocks/platform-updates-template";
import { SetupFactoryOptions } from "../driver";

export const hasUpToDateServiceControl = ({ driver }: SetupFactoryOptions) => {
  driver.mockEndpoint(`https://platformupdate.particular.net/servicecontrol.txt`, {
    body: serviceControlNoPlatformUpdatesNeeded,
  });
  return serviceControlNoPlatformUpdatesNeeded;
};
