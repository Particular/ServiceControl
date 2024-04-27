import content from "../mocks/service-control-no-platform-updates-needed.json";
import {SetupFactoryOptions} from '../driver'

export const hasUpToDateServiceControl = ({ driver }: SetupFactoryOptions) => {
  driver.mockEndpoint(`https://platformupdate.particular.net/servicecontrol.txt`, {
    body: content,
  });
  return content;
};
