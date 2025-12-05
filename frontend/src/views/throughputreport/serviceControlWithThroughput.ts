import { SetupFactoryOptions } from "../../../test/driver";
import * as precondition from "../../../test/preconditions";
import { minimumSCVersionForThroughput } from "@/views/throughputreport/isThroughputSupported";

export const serviceControlWithThroughput = async ({ driver }: SetupFactoryOptions) => {
  await driver.setUp(precondition.hasUpToDateServicePulse);
  await driver.setUp(precondition.hasUpToDateServiceControl);
  await driver.setUp(precondition.errorsDefaultHandler);
  await driver.setUp(precondition.hasCustomChecksEmpty);
  await driver.setUp(precondition.hasEventLogItems);
  await driver.setUp(precondition.hasNoHeartbeatsEndpoints);
  await driver.setUp(precondition.hasServiceControlMainInstance(minimumSCVersionForThroughput));
  await driver.setUp(precondition.hasEndpointSettings([]));
};
