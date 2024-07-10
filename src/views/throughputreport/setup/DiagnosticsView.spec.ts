import { test } from "../../../../test/drivers/vitest/driver";
import { describe, expect } from "vitest";
import * as precondition from "../../../../test/preconditions";
import { useServiceControl } from "@/composables/serviceServiceControl";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import { minimumSCVersionForThroughput } from "@/views/throughputreport/isThroughputSupported";
import flushPromises from "flush-promises";
import { Driver } from "../../../../test/driver";
import { render, screen } from "@testing-library/vue";
import DiagnosticsView from "./DiagnosticsView.vue";
import { createTestingPinia } from "@pinia/testing";
import ConnectionTestResults, { ConnectionSettingsTestResult } from "@/resources/ConnectionTestResults";
import { Transport } from "@/views/throughputreport/transport";
import { userEvent } from "@component-test-utils";

describe("DiagnosticsView tests", () => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;

  async function setup(driver: Driver) {
    await driver.setUp(({ driver }) => precondition.hasServiceControlMainInstance({ driver }, minimumSCVersionForThroughput));
    await driver.setUp(precondition.hasUpToDateServicePulse);
    await driver.setUp(precondition.hasUpToDateServiceControl);
    await driver.setUp(precondition.hasNoErrors);
  }

  async function renderComponent(driver: Driver, transport: Transport = Transport.MSMQ) {
    await setup(driver);
    driver.mockEndpoint(`${serviceControlInstanceUrl}licensing/settings/test`, {
      body: <ConnectionTestResults>{
        transport,
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
    useServiceControlUrls();
    await useServiceControl();
    const { debug } = render(DiagnosticsView, { global: { plugins: [createTestingPinia({ stubActions: false })] } });
    await flushPromises();
    return { debug };
  }

  test("renders audit diagnostics when not a broker and monitoring is not enabled", async ({ driver }) => {
    window.defaultConfig.monitoring_urls = ["!"];
    await renderComponent(driver);
    const use = userEvent.setup();
    await use.click(screen.getByRole("button", { name: /Refresh Connection Test/i }));
    expect(screen.getByText(/Audit diagnostics/i)).toBeInTheDocument();
    expect(screen.queryByText(/Broker diagnostics/i)).toBeNull();
    expect(screen.queryByText(/Monitoring diagnostics/i)).toBeNull();
  });

  test("renders audit and broker diagnostics with monitoring not enabled", async ({ driver }) => {
    window.defaultConfig.monitoring_urls = ["!"];
    await renderComponent(driver, Transport.AmazonSQS);
    const use = userEvent.setup();
    await use.click(screen.getByRole("button", { name: /Refresh Connection Test/i }));
    expect(screen.getByText(/Audit diagnostics/i)).toBeInTheDocument();
    expect(screen.getByText(/Broker diagnostics/i)).toBeInTheDocument();
    expect(screen.queryByText(/Monitoring diagnostics/i)).toBeNull();
  });

  test("renders audit, broker and monitoring diagnostics when all enabled", async ({ driver }) => {
    await driver.setUp(precondition.hasServiceControlMonitoringInstance);
    await driver.setUp(precondition.hasNoDisconnectedEndpoints);
    await renderComponent(driver, Transport.AmazonSQS);
    const use = userEvent.setup();
    await use.click(screen.getByRole("button", { name: /Refresh Connection Test/i }));
    expect(screen.getByText(/Audit diagnostics/i)).toBeInTheDocument();
    expect(screen.getByText(/Broker diagnostics/i)).toBeInTheDocument();
    expect(screen.getByText(/Monitoring diagnostics/i)).toBeInTheDocument();
  });

  test("refreshes diagnostics", async ({ driver }) => {
    await driver.setUp(precondition.hasServiceControlMonitoringInstance);
    await driver.setUp(precondition.hasNoDisconnectedEndpoints);
    await renderComponent(driver, Transport.AmazonSQS);
    const use = userEvent.setup();
    await use.click(screen.getByRole("button", { name: /Refresh Connection Test/i }));

    driver.mockEndpoint(`${serviceControlInstanceUrl}licensing/settings/test`, {
      body: <ConnectionTestResults>{
        transport: Transport.AmazonSQS,
        audit_connection_result: <ConnectionSettingsTestResult>{
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Audit refreshed diagnostics",
        },
        monitoring_connection_result: <ConnectionSettingsTestResult>{
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Monitoring refreshed diagnostics",
        },
        broker_connection_result: <ConnectionSettingsTestResult>{
          connection_successful: true,
          connection_error_messages: [],
          diagnostics: "Broker refreshed diagnostics",
        },
      },
    });

    await use.click(screen.getByRole("button", { name: /Refresh Connection Test/i }));
    expect(screen.getByText(/Audit refreshed diagnostics/i)).toBeInTheDocument();
    expect(screen.getByText(/Broker refreshed diagnostics/i)).toBeInTheDocument();
    expect(screen.getByText(/Monitoring refreshed diagnostics/i)).toBeInTheDocument();
  });
});
