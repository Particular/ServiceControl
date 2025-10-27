import { describe, expect, test } from "vitest";
import * as precondition from "../../test/preconditions";
import { createTestingPinia } from "@pinia/testing";
import { Transport } from "@/views/throughputreport/transport";
import { makeDriverForTests, render, screen, userEvent } from "@component-test-utils";
import { Driver } from "../../test/driver";
import { disableMonitoring } from "../../test/drivers/vitest/setup";
import makeRouter from "@/router";
import { flushPromises, RouterLinkStub } from "@vue/test-utils";
import ThroughputReportView from "@/views/ThroughputReportView.vue";
import Toast from "vue-toastification";
import { serviceControlWithThroughput } from "@/views/throughputreport/serviceControlWithThroughput";
import { useServiceControlStore } from "@/stores/ServiceControlStore";
import { setActivePinia } from "pinia";

describe("EndpointsView tests", () => {
  async function setup() {
    const driver = makeDriverForTests();
    setActivePinia(createTestingPinia({ stubActions: false }));

    await driver.setUp(serviceControlWithThroughput);

    return driver;
  }

  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  async function renderComponent(transport: Transport = Transport.MSMQ, preSetup: (driver: Driver) => Promise<void> = () => Promise.resolve()) {
    disableMonitoring();

    const driver = await setup();
    await preSetup(driver);

    useServiceControlStore();

    const el = document.createElement("div");
    el.id = "modalDisplay";
    document.body.appendChild(el);

    const { debug } = render(ThroughputReportView, {
      container: document.body,
      global: {
        stubs: {
          RouterLink: RouterLinkStub,
        },
        plugins: [makeRouter(), Toast],
        directives: {
          // Add stub for tippy directive
          tippy: () => {},
        },
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
