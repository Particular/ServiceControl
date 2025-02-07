import { SetupFactoryOptions } from "../../../test/driver";
import * as precondition from "../../../test/preconditions";
import { minimumSCVersionForEndpointSettings } from "@/components/heartbeats/isEndpointSettingsSupported";

export const serviceControlWithHeartbeats = async ({ driver }: SetupFactoryOptions) => {
  await driver.setUp(precondition.hasUpToDateServicePulse);
  await driver.setUp(precondition.hasUpToDateServiceControl);
  await driver.setUp(precondition.errorsDefaultHandler);
  await driver.setUp(precondition.hasCustomChecksEmpty);
  await driver.setUp(precondition.hasEventLogItems);
  await driver.setUp(precondition.hasServiceControlMainInstance(minimumSCVersionForEndpointSettings));
  await driver.setUp(precondition.hasNoDisconnectedEndpoints);
  await driver.setUp(precondition.hasServiceControlMonitoringInstance);
};
