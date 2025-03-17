import * as precondition from ".";
import { SetupFactoryOptions } from "../driver";

export const serviceControlWithMonitoring = async ({ driver }: SetupFactoryOptions) => {
  //Service control requests minimum setup. Todo: encapsulate for reuse.

  //http://localhost:33333/api/license
  await driver.setUp(precondition.hasActiveLicense);

  //http://localhost:33333/api/
  await driver.setUp(precondition.hasServiceControlMainInstance());

  //http://localhost:33633
  await driver.setUp(precondition.hasServiceControlMonitoringInstance);

  //https://platformupdate.particular.net/servicecontrol.txt
  await driver.setUp(precondition.hasUpToDateServiceControl);

  //https://platformupdate.particular.net/servicepulse.txt
  await driver.setUp(precondition.hasUpToDateServicePulse);

  //http://localhost:33333/api/errors
  await driver.setUp(precondition.errorsDefaultHandler);

  //http://localhost:33333/api/customchecks
  await driver.setUp(precondition.hasCustomChecksEmpty);

  //http://localhost:33633/monitored-endpoints/disconnected
  await driver.setUp(precondition.hasNoDisconnectedEndpoints);

  //http://localhost:33333/api/eventlogitems
  await driver.setUp(precondition.hasEventLogItems);

  //http://localhost:33333/api/recoverability/groups/Endpoint%20Name
  await driver.setUp(precondition.hasRecoverabilityGroups);

  //http://localhost:33333/api/endpoints
  await driver.setUp(precondition.hasNoHeartbeatsEndpoints);

  //http://localhost:33633/monitored-endpoints
  await driver.setUp(precondition.hasNoMonitoredEndpoints);

  //http://localhost:33333/recoverability/groups/Endpoint%20Instance
  await driver.setUp(precondition.endpointRecoverabilityByInstanceDefaultHandler);

  //http://localhost:33333/recoverability/groups/Endpoint%20Name?classifierFilter=${name} -  the classifierFilter is ignored, this is a default handler for the route.
  await driver.setUp(precondition.endpointRecoverabilityByNameDefaultHandler);

  //OPTIONS VERB against monitoring instance http://localhost:33633/ - this is used for enabling deleting an instance from the endpoint details page - instances panel
  await driver.setUp(precondition.serviceControlMonitoringOptions);

  //http://localhost:33333/api/configuration default handler
  await driver.setUp(precondition.serviceControlConfigurationDefaultHandler);

  //http://localhost:33333/api/recoverability/classifiers default handler
  await driver.setUp(precondition.recoverabilityClassifiers);

  //http://localhost:33333/api/recoverability/history default handler
  await driver.setUp(precondition.recoverabilityHistoryDefaultHandler);

  //http://localhost:33333/api/edit/config default handler
  await driver.setUp(precondition.recoverabilityEditConfigDefaultHandler);

  //http://localhost:33333/api/errors/groups{/:classifier}? default handler
  await driver.setUp(precondition.archivedGroupsWithClassifierDefaulthandler);

  //http://localhost:33333/api/recoverability/groups{/:classifier} default handler
  await driver.setUp(precondition.recoverabilityGroupsWithClassifierDefaulthandler);

  //Default handler for /api/licensing/report/available
  await driver.setUp(precondition.hasLicensingReportAvailable());

  //Default handler for /api/licensing/endpoints
  await driver.setUp(precondition.hasLicensingEndpoints());

  //Default handler for /api/licensing/settings/test
  await driver.setUp(precondition.hasLicensingSettingTest());

  await driver.setUp(precondition.hasEndpointSettings([]));

  //default handler for /api/redirects
  await driver.setUp(precondition.redirectsDefaultHandler);

  //default handler for /api/queues/addresses
  await driver.setUp(precondition.knownQueuesDefaultHandler);
};
