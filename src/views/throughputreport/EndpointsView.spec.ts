import { describe, expect, test } from "vitest";
import * as precondition from "../../../test/preconditions";
import { useServiceControl } from "@/composables/serviceServiceControl";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import { createTestingPinia } from "@pinia/testing";
import { Transport } from "@/views/throughputreport/transport";
import { makeDriverForTests, render, screen, userEvent } from "@component-test-utils";
import { Driver } from "../../../test/driver";
import { disableMonitoring } from "../../../test/drivers/vitest/setup";
import makeRouter from "@/router";
import { flushPromises, RouterLinkStub } from "@vue/test-utils";
import EndpointsView from "./EndpointsView.vue";
import { serviceControlWithThroughput } from "@/views/throughputreport/serviceControlWithThroughput";

describe("EndpointsView tests", () => {
  async function setup(transport: Transport) {
    const driver = makeDriverForTests();

    await driver.setUp(serviceControlWithThroughput);
    await driver.setUp(precondition.hasLicensingSettingTest({ transport }));

    return driver;
  }

  async function renderComponent(transport: Transport = Transport.MSMQ, preSetup: (driver: Driver) => Promise<void> = () => Promise.resolve()) {
    disableMonitoring();

    const driver = await setup(transport);
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

  test("instructions by default are not showing", async () => {
    await renderComponent();

    expect(screen.queryByText(/Show Endpoint Types meaning/i)).toBeInTheDocument();
  });

  test("show instructions", async () => {
    await renderComponent();

    const use = userEvent.setup();

    await use.click(screen.getByRole("link", { name: /Show Endpoint Types meaning/i }));

    expect(screen.queryByText(/Hide Endpoint Types meaning/i)).toBeInTheDocument();
  });

  test("broker displays the two tabs", async () => {
    await renderComponent(Transport.AmazonSQS);

    expect(screen.getByText(/Detected Broker Queues/i)).toBeInTheDocument();
  });

  test("non broker displays only one tabs", async () => {
    await renderComponent();

    expect(screen.queryByText(/Detected Broker Queues/i)).not.toBeInTheDocument();
  });
});
