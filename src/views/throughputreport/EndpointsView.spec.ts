import { describe, expect, test } from "vitest";
import * as precondition from "../../../test/preconditions";
import { useServiceControl } from "@/composables/serviceServiceControl";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import { minimumSCVersionForThroughput } from "@/views/throughputreport/isThroughputSupported";
import flushPromises from "flush-promises";
import { createTestingPinia } from "@pinia/testing";
import { Transport } from "@/views/throughputreport/transport";
import { makeDriverForTests, render, screen, userEvent } from "@component-test-utils";
import { Driver } from "../../../test/driver";
import { disableMonitoring } from "../../../test/drivers/vitest/setup";
import SetupView from "./SetupView.vue";
import ConnectionTestResults, { ConnectionSettingsTestResult } from "@/resources/ConnectionTestResults";
import makeRouter from "@/router";
import { RouterLinkStub } from "@vue/test-utils";
import EndpointsView from "./EndpointsView.vue";

describe("EndpointsView tests", () => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;

  async function setup() {
    const driver = makeDriverForTests();

    await driver.setUp(precondition.hasUpToDateServicePulse);
    await driver.setUp(precondition.hasUpToDateServiceControl);
    await driver.setUp(precondition.errorsDefaultHandler);
    await driver.setUp(precondition.hasNoFailingCustomChecks);
    await driver.setUp(precondition.hasEventLogItems);
    await driver.setUp(precondition.hasNoHeartbeatsEndpoints);
    await driver.setUp(precondition.hasServiceControlMainInstance(minimumSCVersionForThroughput));
    driver.mockEndpoint(`${serviceControlInstanceUrl}licensing/settings/test`, {
      body: <ConnectionTestResults>{
        transport: Transport.MSMQ,
        audit_connection_result: <ConnectionSettingsTestResult>{
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Audit diagnostics",
        },
        monitoring_connection_result: <ConnectionSettingsTestResult>{
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Monitoring diagnostics",
        },
        broker_connection_result: <ConnectionSettingsTestResult>{
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Broker diagnostics",
        },
      },
    });

    return driver;
  }

  async function renderComponent(transport: Transport = Transport.MSMQ, preSetup: (driver: Driver) => Promise<void> = () => Promise.resolve()) {
    disableMonitoring();

    const driver = await setup();
    await preSetup(driver);

    useServiceControlUrls();
    await useServiceControl();
    const { debug } = render(EndpointsView, {
      global: {
        stubs: {
          RouterLink: RouterLinkStub,
        },
        plugins: [makeRouter(), createTestingPinia({ stubActions: false })],
      },
    });
    await flushPromises();

    return { debug, driver };
  }

  test("instructions by default are showing", async () => {
    await renderComponent();

    expect(screen.queryByText(/Hide Endpoint Types meaning/i)).toBeInTheDocument();
  });

  test("hide instructions", async () => {
    await renderComponent();

    const use = userEvent.setup();

    await use.click(screen.getByRole("link", { name: /Hide Endpoint Types meaning/i }));

    expect(screen.queryByText(/Show Endpoint Types meaning/i)).toBeInTheDocument();
  });
});
