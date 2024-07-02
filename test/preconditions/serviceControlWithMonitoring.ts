import { monitoredEndpointList } from "@/../test/mocks/monitored-endpoint-template";
import * as precondition from ".";
import { SetupFactoryOptions } from "../driver";

export const serviceControlWithMonitoring = async ({ driver }: SetupFactoryOptions) => {
  //Service control requests minimum setup. Todo: encapsulate for reuse.

  //http://localhost:33333/api/license
  await driver.setUp(precondition.hasActiveLicense);

  //http://localhost:33333/api/
  await driver.setUp(precondition.hasServiceControlMainInstance);

  //http://localhost:33633
  await driver.setUp(precondition.hasServiceControlMonitoringInstance);

  //https://platformupdate.particular.net/servicecontrol.txt
  await driver.setUp(precondition.hasUpToDateServiceControl);

  //https://platformupdate.particular.net/servicepulse.txt
  await driver.setUp(precondition.hasUpToDateServicePulse);

  //http://localhost:33333/api/errors
  await driver.setUp(precondition.hasNoErrors);

  //http://localhost:33333/api/customchecks
  await driver.setUp(precondition.hasNoFailingCustomChecks);

  //http://localhost:33633/monitored-endpoints/disconnected
  await driver.setUp(precondition.hasNoDisconnectedEndpoints);

  //http://localhost:33333/api/eventlogitems
  await driver.setUp(precondition.hasEventLogItems);

  //http://localhost:33333/api/heartbeats/stats
  await driver.setUp(precondition.hasFiveActiveOneFailingHeartbeats);

  //http://localhost:33333/api/recoverability/groups/Endpoint%20Name
  await driver.setUp(precondition.hasRecoverabilityGroups);

  //http://localhost:33333/api/endpoints
  await driver.setUp(precondition.hasNoHeartbeatsEndpoints);

  //http://localhost:33633/monitored-endpoints
  await driver.setUp(precondition.hasNoMonitoredEndpoints);

  //http://localhost:33333/recoverability/groups/Endpoint%20Instance
  await driver.setUp(precondition.endpointRecoverabilityByInstanceDefaultHandler)

  //http://localhost:33333/recoverability/groups/Endpoint%20Name?classifierFilter=${name} -  the classifierFilter is ignored, this is a default handler for the route.
  await driver.setUp(precondition.endpointRecoverabilityByNameDefaultHandler);  

 //OPTIONS VERB agaisnt monitoring instance http://localhost:33633/ - this is used for enabling deleting an instance from the endpoint details page - instances panel
 await driver.setUp(precondition.serviceControlMonitoringOptions)
};
