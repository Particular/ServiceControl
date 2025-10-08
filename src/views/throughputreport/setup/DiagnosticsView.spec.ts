import { describe, expect, test } from "vitest";
import * as precondition from "../../../../test/preconditions";
import { useServiceControl } from "@/composables/serviceServiceControl";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import { minimumSCVersionForThroughput } from "@/views/throughputreport/isThroughputSupported";
import DiagnosticsView from "./DiagnosticsView.vue";
import { createTestingPinia } from "@pinia/testing";
import { Transport } from "@/views/throughputreport/transport";
import { makeDriverForTests, userEvent, render, screen } from "@component-test-utils";
import { Driver } from "../../../../test/driver";
import { disableMonitoring } from "../../../../test/drivers/vitest/setup";

describe("DiagnosticsView tests", () => {
  async function setup() {
    const driver = makeDriverForTests();

    await driver.setUp(precondition.hasServiceControlMainInstance(minimumSCVersionForThroughput));
    await driver.setUp(precondition.hasUpToDateServicePulse);
    await driver.setUp(precondition.hasUpToDateServiceControl);
    await driver.setUp(precondition.errorsDefaultHandler);

    return driver;
  }

  async function renderComponent(transport: Transport = Transport.MSMQ, preSetup: (driver: Driver) => Promise<void> = () => Promise.resolve()) {
    const driver = await setup();

    await preSetup(driver);

    await driver.setUp(
      precondition.hasLicensingSettingTest({
        transport,
        audit_connection_result: {
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Audit diagnostics",
        },
        monitoring_connection_result: {
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Monitoring diagnostics",
        },
        broker_connection_result: {
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Broker diagnostics",
        },
      })
    );

    useServiceControlUrls();
    await useServiceControl();

    const { debug } = render(DiagnosticsView, { global: { plugins: [createTestingPinia({ stubActions: false })] } });

    return { debug, driver };
  }

  test("renders audit diagnostics when not a broker and monitoring is not enabled", async () => {
    disableMonitoring();

    await renderComponent();
    const use = userEvent.setup();
    await use.click(screen.getByRole("button", { name: /Refresh Connection Test/i }));
    expect(screen.getByText(/Audit diagnostics/i)).toBeInTheDocument();
    expect(screen.queryByText(/Broker diagnostics/i)).toBeNull();
    expect(screen.queryByText(/Monitoring diagnostics/i)).toBeNull();
  });

  test("renders audit and broker diagnostics with monitoring not enabled", async () => {
    disableMonitoring();

    await renderComponent(Transport.AmazonSQS);
    const use = userEvent.setup();
    await use.click(screen.getByRole("button", { name: /Refresh Connection Test/i }));
    expect(screen.getByText(/Audit diagnostics/i)).toBeInTheDocument();
    expect(screen.getByText(/Broker diagnostics/i)).toBeInTheDocument();
    expect(screen.queryByText(/Monitoring diagnostics/i)).toBeNull();
  });

  test("renders audit, broker and monitoring diagnostics when all enabled", async () => {
    await renderComponent(Transport.AmazonSQS, async (driver) => {
      await driver.setUp(precondition.hasServiceControlMonitoringInstance);
      await driver.setUp(precondition.hasNoDisconnectedEndpoints);
    });
    const use = userEvent.setup();
    await use.click(screen.getByRole("button", { name: /Refresh Connection Test/i }));
    expect(screen.getByText(/Audit diagnostics/i)).toBeInTheDocument();
    expect(screen.getByText(/Broker diagnostics/i)).toBeInTheDocument();
    expect(screen.getByText(/Monitoring diagnostics/i)).toBeInTheDocument();
  });

  test("refreshes diagnostics", async () => {
    const { driver } = await renderComponent(Transport.AmazonSQS, async (driver) => {
      await driver.setUp(precondition.hasServiceControlMonitoringInstance);
      await driver.setUp(precondition.hasNoDisconnectedEndpoints);
    });
    const use = userEvent.setup();
    await use.click(screen.getByRole("button", { name: /Refresh Connection Test/i }));

    await driver.setUp(
      precondition.hasLicensingSettingTest({
        transport: Transport.AmazonSQS,
        audit_connection_result: {
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Audit refreshed diagnostics",
        },
        monitoring_connection_result: {
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Monitoring refreshed diagnostics",
        },
        broker_connection_result: {
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Broker refreshed diagnostics",
        },
      })
    );

    await use.click(screen.getByRole("button", { name: /Refresh Connection Test/i }));
    expect(screen.getByText(/Audit refreshed diagnostics/i)).toBeInTheDocument();
    expect(screen.getByText(/Broker refreshed diagnostics/i)).toBeInTheDocument();
    expect(screen.getByText(/Monitoring refreshed diagnostics/i)).toBeInTheDocument();
  });
});
