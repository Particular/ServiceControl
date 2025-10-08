import { describe, expect, test } from "vitest";
import * as precondition from "../../test/preconditions";
import { useServiceControl } from "@/composables/serviceServiceControl";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import { createTestingPinia } from "@pinia/testing";
import { Transport } from "@/views/throughputreport/transport";
import { makeDriverForTests, render, screen, userEvent } from "@component-test-utils";
import { Driver } from "../../test/driver";
import { disableMonitoring } from "../../test/drivers/vitest/setup";
import makeRouter from "@/router";
import { RouterLinkStub } from "@vue/test-utils";
import ThroughputReportView from "@/views/ThroughputReportView.vue";
import Toast from "vue-toastification";
import { serviceControlWithThroughput } from "@/views/throughputreport/serviceControlWithThroughput";
import flushPromises from "flush-promises";

describe("EndpointsView tests", () => {
  async function setup() {
    const driver = makeDriverForTests();

    await driver.setUp(serviceControlWithThroughput);

    return driver;
  }

  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  async function renderComponent(transport: Transport = Transport.MSMQ, preSetup: (driver: Driver) => Promise<void> = () => Promise.resolve()) {
    disableMonitoring();

    const driver = await setup();
    await preSetup(driver);

    useServiceControlUrls();
    await useServiceControl();
    const el = document.createElement("div");
    el.id = "modalDisplay";
    document.body.appendChild(el);

    const { debug } = render(ThroughputReportView, {
      container: document.body,
      global: {
        stubs: {
          RouterLink: RouterLinkStub,
        },
        plugins: [makeRouter(), Toast, createTestingPinia({ stubActions: false })],
      },
    });
    await flushPromises();

    return { debug, driver };
  }

  describe("when minimum requirements", () => {
    test("are met", async () => {
      await renderComponent(Transport.AmazonSQS, async (driver) => {
        await driver.setUp(precondition.hasLicensingReportAvailable());
      });

      expect(screen.queryByText(/the minimum version of servicecontrol required to enable the usage feature is/i)).not.toBeInTheDocument();
    });

    test("are not met, requirements warning is displayed", async () => {
      await renderComponent(Transport.AmazonSQS, async (driver) => {
        await driver.setUp(precondition.hasServiceControlMainInstance("1.0.0"));
        await driver.setUp(precondition.hasLicensingReportAvailable());
      });

      expect(screen.getByText(/the minimum version of servicecontrol required to enable the usage feature is/i)).toBeInTheDocument();
    });
  });

  describe("when report", () => {
    test("is available", async () => {
      await renderComponent(Transport.AmazonSQS, async (driver) => {
        await driver.setUp(precondition.hasLicensingReportAvailable());
      });
      expect(screen.getByRole("button", { name: /Download Report/i })).toBeEnabled();
    });

    test("is unavailable", async () => {
      const reason = "report testing that is not available";
      await renderComponent(Transport.AmazonSQS, async (driver) => {
        await driver.setUp(precondition.hasLicensingReportAvailable({ report_can_be_generated: false, reason: reason }));
      });

      expect(screen.getByRole("button", { name: /Download Report/i })).toBeDisabled();
      expect(screen.getByText(reason)).toBeInTheDocument();
    });
  });

  describe("when download report is clicked", () => {
    test("and no warnings, download happens", async () => {
      URL.createObjectURL = () => "";
      const fileName = "hello_john.json";

      await renderComponent(Transport.AmazonSQS, async (driver) => {
        await driver.setUp(precondition.hasLicensingReportAvailable());
        await driver.setUp(precondition.hasLicensingEndpoints([{ name: "foo", is_known_endpoint: false, user_indicator: "something", max_daily_throughput: 0 }]));
        driver.mockEndpoint(`${window.defaultConfig.service_control_url}licensing/report/file`, {
          body: {},
          headers: {
            "Content-Disposition": `attachment; filename="${fileName}"`,
          },
        });
      });

      const use = userEvent.setup();

      await use.click(screen.getByRole("button", { name: /Download Report/i }));
      expect(screen.queryAllByText(new RegExp(`Please email '${fileName}' to your account manager`)).length).toBeGreaterThanOrEqual(1);
    });

    test("and there are warnings, dialog is displayed", async () => {
      await renderComponent(Transport.AmazonSQS, async (driver) => {
        await driver.setUp(precondition.hasLicensingReportAvailable());
        await driver.setUp(precondition.hasLicensingEndpoints([{ name: "foo", is_known_endpoint: false, user_indicator: "", max_daily_throughput: 0 }]));
      });

      const use = userEvent.setup();

      await use.click(screen.getByRole("button", { name: /Download Report/i }));
      expect(screen.getByText("Not all endpoints/queues have an Endpoint Type set")).toBeInTheDocument();
    });
  });
});
