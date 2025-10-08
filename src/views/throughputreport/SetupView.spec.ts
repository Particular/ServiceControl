import { describe, expect, test } from "vitest";
import * as precondition from "../../../test/preconditions";
import { useServiceControl } from "@/composables/serviceServiceControl";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import { minimumSCVersionForThroughput } from "@/views/throughputreport/isThroughputSupported";
import { createTestingPinia } from "@pinia/testing";
import { Transport } from "@/views/throughputreport/transport";
import { makeDriverForTests, render, screen } from "@component-test-utils";
import { Driver } from "../../../test/driver";
import { disableMonitoring } from "../../../test/drivers/vitest/setup";
import SetupView from "./SetupView.vue";
import { ConnectionSettingsTestResult } from "@/resources/ConnectionTestResults";
import makeRouter from "@/router";
import { RouterLinkStub } from "@vue/test-utils";
import { serviceControlWithThroughput } from "@/views/throughputreport/serviceControlWithThroughput";

describe("SetupView tests", () => {
  async function setup() {
    const driver = makeDriverForTests();

    await driver.setUp(serviceControlWithThroughput);

    return driver;
  }

  async function renderComponent(transport: Transport = Transport.MSMQ, preSetup: (driver: Driver) => Promise<void> = () => Promise.resolve()) {
    const driver = await setup();

    await driver.setUp(
      precondition.hasLicensingSettingTest({
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
      })
    );

    await preSetup(driver);

    useServiceControlUrls();
    await useServiceControl();
    const { debug } = render(SetupView, {
      global: {
        stubs: {
          RouterLink: RouterLinkStub,
        },
        plugins: [makeRouter(), createTestingPinia({ stubActions: false })],
      },
    });

    return { debug, driver };
  }

  describe("when minimum requirements", () => {
    test("are met", async () => {
      disableMonitoring();

      await renderComponent();

      expect(screen.queryByText(/the minimum version of servicecontrol required to enable the usage feature is \./i)).not.toBeInTheDocument();
    });

    test("are not met, requirements warning is displayed", async () => {
      disableMonitoring();

      await renderComponent(Transport.MSMQ, async (driver) => {
        await driver.setUp(precondition.hasServiceControlMainInstance("1.0.0"));
      });

      expect(screen.getByText(/the minimum version of servicecontrol required to enable the usage feature is \./i)).toBeInTheDocument();
    });
  });

  describe("when not a broker", () => {
    test("without monitoring", async () => {
      disableMonitoring();
      await renderComponent();

      expect(screen.getByText(/Successfully connected to Audit instance/i)).toBeInTheDocument();
      expect(screen.queryByText(/Successfully connected to Monitoring/i)).not.toBeInTheDocument();
    });

    test("with monitoring", async () => {
      await renderComponent(Transport.MSMQ, async (driver) => {
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.hasServiceControlMainInstance(minimumSCVersionForThroughput));
        await driver.setUp(
          precondition.hasLicensingSettingTest({
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
          })
        );
      });

      expect(screen.getByText(/Successfully connected to Audit/i)).toBeInTheDocument();
      expect(screen.getByText(/Successfully connected to Monitoring/i)).toBeInTheDocument();
    });
  });

  describe("when a broker", () => {
    test("display success", async () => {
      disableMonitoring();
      await renderComponent(Transport.AmazonSQS);

      expect(screen.getByText(/Successfully connected to Amazon SQS for usage collection/i)).toBeInTheDocument();
    });

    test("display failure", async () => {
      disableMonitoring();
      await renderComponent(Transport.AmazonSQS, async (driver) => {
        await driver.setUp(
          precondition.hasLicensingSettingTest({
            transport: Transport.AmazonSQS,
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
              connection_successful: false,
              connection_error_messages: [],
              diagnostics: "Broker diagnostics",
            },
          })
        );
      });

      expect(screen.getByText(/The connection to Amazon SQS was not successful/i)).toBeInTheDocument();
    });
  });
});
