import * as precondition from ".";
import { SetupFactoryOptions } from "../driver";
import EndpointThroughputSummary from "@/resources/EndpointThroughputSummary";
import ReportGenerationState from "@/resources/ReportGenerationState";
import ConnectionTestResults, { ConnectionSettingsTestResult } from "@/resources/ConnectionTestResults";

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

  //Default handler for /api/licensing/report/available
  driver.mockEndpoint(`${window.defaultConfig.service_control_url}licensing/report/available`, {
    body: <ReportGenerationState>{
      transport: "LearningTransport",
      report_can_be_generated: true,
      reason: "",
    },
    method: "get",
    status: 200,
  });

  //Default handler for /api/licensing/endpoints
  driver.mockEndpoint(`${window.defaultConfig.service_control_url}licensing/endpoints`, {
    body: [<EndpointThroughputSummary>{
      name: "",
      is_known_endpoint: true,
      user_indicator: "",
      max_daily_throughput: 10,
    }],
    method: "get",
    status: 200,
  });

  //Default handler for /api/licensing/settings/test
  driver.mockEndpoint(`${window.defaultConfig.service_control_url}licensing/settings/test`, {
    body: <ConnectionTestResults>{
      transport: "",
      audit_connection_result: <ConnectionSettingsTestResult>{
        connection_successful: true,
        connection_error_messages: [],
        diagnostics: "",
      },
      monitoring_connection_result: <ConnectionSettingsTestResult>{
        connection_successful: true,
        connection_error_messages: [],
        diagnostics: "",
      },
      broker_connection_result: <ConnectionSettingsTestResult>{
        connection_successful: true,
        connection_error_messages: [],
        diagnostics: "",
      },
    },
    method: "get",
    status: 200,
  });
};
