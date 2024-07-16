import { describe, expect, test } from "vitest";
import * as precondition from "../../../../test/preconditions";
import { useServiceControl } from "@/composables/serviceServiceControl";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import { minimumSCVersionForThroughput } from "@/views/throughputreport/isThroughputSupported";
import flushPromises from "flush-promises";
import { Transport } from "@/views/throughputreport/transport";
import { makeDriverForTests, render, screen } from "@component-test-utils";
import { Driver } from "../../../../test/driver";
import { disableMonitoring } from "../../../../test/drivers/vitest/setup";
import DetectedListView, { DetectedListViewProps } from "@/views/throughputreport/endpoints/DetectedListView.vue";
import { DataSource } from "@/views/throughputreport/endpoints/dataSource";

describe("DetectedListView tests", () => {
  async function setup() {
    const driver = makeDriverForTests();

    await driver.setUp(precondition.hasUpToDateServicePulse);
    await driver.setUp(precondition.hasUpToDateServiceControl);
    await driver.setUp(precondition.errorsDefaultHandler);
    await driver.setUp(precondition.hasNoFailingCustomChecks);
    await driver.setUp(precondition.hasEventLogItems);
    await driver.setUp(precondition.hasNoHeartbeatsEndpoints);
    await driver.setUp(precondition.hasServiceControlMainInstance(minimumSCVersionForThroughput));
    await driver.setUp(precondition.hasLicensingSettingTest({ transport: Transport.AmazonSQS }));

    return driver;
  }

  async function renderComponent(props: Partial<DetectedListViewProps> = {}, preSetup: (driver: Driver) => Promise<void> = () => Promise.resolve()) {
    disableMonitoring();

    const driver = await setup();
    await preSetup(driver);

    useServiceControlUrls();
    await useServiceControl();
    const el = document.createElement("div");
    el.id = "modalDisplay";
    document.body.appendChild(el);
    const { debug } = render(DetectedListView, {
      global: {
        directives: {
          tooltip: {},
        },
      },
      container: document.body,
      props: {
        ...{
          columnTitle: "Name",
          showEndpointTypePlaceholder: true,
          indicatorOptions: [],
          source: DataSource.Broker,
        },
        ...props,
      },
    });
    await flushPromises();

    return { debug, driver };
  }

  describe("We only display", () => {
    test("queues when broker is selected", async () => {
      await renderComponent({}, async (driver) => {
        await driver.setUp(
          precondition.hasLicensingEndpoints([
            { name: "I am a queue", is_known_endpoint: false, user_indicator: "", max_daily_throughput: 10 },
            { name: "I am an endpoint", is_known_endpoint: true, user_indicator: "", max_daily_throughput: 100 },
          ])
        );
      });

      expect(screen.getByText(/I am a queue/i)).toBeInTheDocument();
      expect(screen.queryByText(/I am an endpoint/i)).not.toBeInTheDocument();
    });

    test("endpoints when well known endpoint is selected", async () => {
      await renderComponent({ source: DataSource.WellKnownEndpoint }, async (driver) => {
        await driver.setUp(
          precondition.hasLicensingEndpoints([
            { name: "I am a queue", is_known_endpoint: false, user_indicator: "", max_daily_throughput: 10 },
            { name: "I am an endpoint", is_known_endpoint: true, user_indicator: "", max_daily_throughput: 100 },
          ])
        );
      });

      expect(screen.getByText(/I am an endpoint/i)).toBeInTheDocument();
      expect(screen.queryByText(/I am a queue/i)).not.toBeInTheDocument();
    });
  });
});
