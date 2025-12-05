import { describe, expect, test } from "vitest";
import * as precondition from "../../../../test/preconditions";
import { Transport } from "@/views/throughputreport/transport";
import { makeDriverForTests, render, screen, userEvent } from "@component-test-utils";
import { Driver, SetupFactoryOptions } from "../../../../test/driver";
import { disableMonitoring } from "../../../../test/drivers/vitest/setup";
import DetectedListView, { DetectedListViewProps } from "@/views/throughputreport/endpoints/DetectedListView.vue";
import { DataSource } from "@/views/throughputreport/endpoints/dataSource";
import { UserIndicator } from "@/views/throughputreport/endpoints/userIndicator";
import { within } from "@testing-library/vue";
import UpdateUserIndicator from "@/resources/UpdateUserIndicator";
import { serviceControlWithThroughput } from "@/views/throughputreport/serviceControlWithThroughput";
import { flushPromises } from "@vue/test-utils";
import { useServiceControlStore } from "@/stores/ServiceControlStore";
import { setActivePinia } from "pinia";
import { getDefaultConfig } from "@/defaultConfig";
import { createTestingPinia } from "@pinia/testing";

describe("DetectedListView tests", () => {
  async function setup() {
    const driver = makeDriverForTests();
    setActivePinia(createTestingPinia({ stubActions: false }));

    await driver.setUp(serviceControlWithThroughput);
    await driver.setUp(precondition.hasLicensingSettingTest({ transport: Transport.AmazonSQS }));

    return driver;
  }

  async function renderComponent(props: Partial<DetectedListViewProps> = {}, preSetup: (driver: Driver) => Promise<void> = () => Promise.resolve()) {
    disableMonitoring();

    const driver = await setup();
    await preSetup(driver);

    useServiceControlStore();

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
          ariaLabel: "my endpoints",
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

  describe("filtering scenarios", () => {
    function getFilterNameElement() {
      return screen.getByRole("searchbox", { name: /Filter by name/i }) as HTMLTextAreaElement;
    }

    function getFilterNameTypeElement() {
      return screen.getByRole("combobox", { name: /Filter name type/i }) as HTMLSelectElement;
    }

    function getFilterUnsetCheckboxElement() {
      return screen.getByRole("checkbox", { name: /Show only not set Endpoint Types/i }) as HTMLInputElement;
    }

    test("by name", async () => {
      await renderComponent({ source: DataSource.WellKnownEndpoint }, async (driver) => {
        await driver.setUp(
          precondition.hasLicensingEndpoints([
            ...[...Array(10).keys()].map((i) => ({ name: `Alpha${i}`, is_known_endpoint: true, user_indicator: "", max_daily_throughput: i })),
            ...[...Array(10).keys()].map((i) => ({ name: `${i}Beta`, is_known_endpoint: true, user_indicator: "", max_daily_throughput: i })),
            ...[...Array(10).keys()].map((i) => ({ name: `${i}Delta${i}`, is_known_endpoint: true, user_indicator: "", max_daily_throughput: i })),
          ])
        );
      });

      const user = userEvent.setup();
      const filterNameElement = getFilterNameElement();
      await user.type(filterNameElement, "Alpha");

      expect(screen.queryAllByText(/Alpha\d/i).length).toBe(10);
      expect(screen.queryAllByText(/\dBeta/i).length).toBe(0);
      expect(screen.queryAllByText(/\dDelta\d/i).length).toBe(0);

      const filterNameTypeElement = getFilterNameTypeElement();
      await user.selectOptions(filterNameTypeElement, "Ends with");
      await user.clear(filterNameElement);
      await user.type(filterNameElement, "Beta");

      expect(screen.queryAllByText(/\dBeta/i).length).toBe(10);
      expect(screen.queryAllByText(/Alpha\d/i).length).toBe(0);
      expect(screen.queryAllByText(/\dDelta\d/i).length).toBe(0);

      await user.selectOptions(filterNameTypeElement, "Contains");
      await user.clear(filterNameElement);
      await user.type(filterNameElement, "Delta");

      expect(screen.queryAllByText(/\dDelta\d/i).length).toBe(10);
      expect(screen.queryAllByText(/\dBeta/i).length).toBe(0);
      expect(screen.queryAllByText(/Alpha\d/i).length).toBe(0);
    });

    test("by unset only", async () => {
      await renderComponent({ source: DataSource.Broker, indicatorOptions: [UserIndicator.NServiceBusEndpoint] }, async (driver) => {
        await driver.setUp(
          precondition.hasLicensingEndpoints([
            ...[...Array(10).keys()].map((i) => ({ name: `Alpha${i}`, is_known_endpoint: false, user_indicator: "", max_daily_throughput: i })),
            ...[...Array(10).keys()].map((i) => ({ name: `${i}Beta`, is_known_endpoint: false, user_indicator: UserIndicator.NServiceBusEndpoint, max_daily_throughput: i })),
            ...[...Array(10).keys()].map((i) => ({ name: `${i}Delta${i}`, is_known_endpoint: false, user_indicator: "", max_daily_throughput: i })),
          ])
        );
      });

      const user = userEvent.setup();
      const filterCheckboxElement = getFilterUnsetCheckboxElement();
      await user.click(filterCheckboxElement);

      expect(screen.queryAllByText(/Alpha\d/i).length).toBe(10);
      expect(screen.queryAllByText(/\dBeta/i).length).toBe(0);
      expect(screen.queryAllByText(/\dDelta\d/i).length).toBe(10);
    });

    test("by combination of all", async () => {
      const tableName = "Special table";

      await renderComponent({ source: DataSource.Broker, indicatorOptions: [UserIndicator.NServiceBusEndpoint], ariaLabel: tableName }, async (driver) => {
        await driver.setUp(
          precondition.hasLicensingEndpoints([
            ...[...Array(5).keys()].map((i) => ({ name: `${i}Beta`, is_known_endpoint: false, user_indicator: UserIndicator.PlannedToDecommission, max_daily_throughput: i })),
            ...[...Array(8).keys()].map((i) => ({ name: `${i}Beta`, is_known_endpoint: false, user_indicator: UserIndicator.PlannedToDecommission, max_daily_throughput: i })),
            ...[...Array(2).keys()].map((i) => ({ name: `${i}Delta${i}`, is_known_endpoint: false, user_indicator: UserIndicator.PlannedToDecommission, max_daily_throughput: i })),
            { name: "boo", is_known_endpoint: false, user_indicator: "", max_daily_throughput: 11 },
          ])
        );
      });

      const user = userEvent.setup();
      const filterCheckboxElement = getFilterUnsetCheckboxElement();
      await user.click(filterCheckboxElement);

      const filterNameTypeElement = getFilterNameTypeElement();
      const filterNameElement = getFilterNameElement();
      await user.selectOptions(filterNameTypeElement, "Begins with");
      await user.clear(filterNameElement);
      await user.type(filterNameElement, "boo");

      const table = screen.getByRole("table", { name: tableName });

      expect(within(table).getAllByRole("row").length).toBe(1 + 1 /* includes header row */);
    });
  });

  describe("sorting by", () => {
    test("throughput", async () => {
      const tableName = "Special table";
      const dataLength = 5;

      await renderComponent({ source: DataSource.Broker, indicatorOptions: [UserIndicator.NServiceBusEndpoint], ariaLabel: tableName }, async (driver) => {
        await driver.setUp(precondition.hasLicensingEndpoints([...[...Array(dataLength).keys()].map((i) => ({ name: `${i}Beta`, is_known_endpoint: false, user_indicator: UserIndicator.PlannedToDecommission, max_daily_throughput: i }))]));
      });

      const user = userEvent.setup();
      await user.click(screen.getByRole("button", { name: /Sort by/i }));
      await user.click(screen.getByRole("link", { name: "throughput" }));

      const table = screen.getByRole("table", { name: tableName }) as HTMLTableElement;

      let throughput = 0;
      for (const row of within(table).getAllByRole("row").slice(1)) {
        expect(within(row).getByRole("cell", { name: "maximum usage throughput" }).textContent).toBe(`${throughput++}`);
      }

      await user.click(screen.getByRole("button", { name: /Sort by/i }));
      await user.click(screen.getByRole("link", { name: "throughput (Descending)" }));

      throughput = dataLength - 1;
      for (const row of within(table).getAllByRole("row").slice(1)) {
        expect(within(row).getByRole("cell", { name: "maximum usage throughput" }).textContent).toBe(`${throughput--}`);
      }
    });

    test("name", async () => {
      const tableName = "Special table";
      const unsortedNames = ["basilisk", "octopus", "hamster", "anteater", "porcupine", "gazelle", "seal", "lynx", "crocodile", "mountain goat", "yak", "polar bear", "horse", "gorilla", "zebu", "salamander", "alligator", "vicuna", "goat", "bunny"];
      const lexicallySortedNames = [
        "alligator",
        "anteater",
        "basilisk",
        "bunny",
        "crocodile",
        "gazelle",
        "goat",
        "gorilla",
        "hamster",
        "horse",
        "lynx",
        "mountain goat",
        "octopus",
        "polar bear",
        "porcupine",
        "salamander",
        "seal",
        "vicuna",
        "yak",
        "zebu",
      ];

      await renderComponent({ source: DataSource.Broker, indicatorOptions: [UserIndicator.NServiceBusEndpoint], ariaLabel: tableName }, async (driver) => {
        await driver.setUp(precondition.hasLicensingEndpoints([...[...unsortedNames].map((name, idx) => ({ name, is_known_endpoint: false, user_indicator: UserIndicator.PlannedToDecommission, max_daily_throughput: idx }))]));
      });

      const user = userEvent.setup();
      await user.click(screen.getByRole("button", { name: /Sort by/i }));
      await user.click(screen.getByRole("link", { name: "name" }));

      const table = screen.getByRole("table", { name: tableName });

      //let orderedNames = unsortedNames.sort((a, b) => a.localeCompare(b));
      let idx = 0;
      for (const row of within(table).getAllByRole("row").slice(1)) {
        expect(within(row).getByRole("cell", { name: "name" }).textContent).toBe(lexicallySortedNames[idx++]);
      }

      await user.click(screen.getByRole("button", { name: /Sort by/i }));
      await user.click(screen.getByRole("link", { name: "name (Descending)" }));

      const reverseLexicallySortedNames = lexicallySortedNames.reverse();
      idx = 0;
      for (const row of within(table).getAllByRole("row").slice(1)) {
        expect(within(row).getByRole("cell", { name: "name" }).textContent).toBe(reverseLexicallySortedNames[idx++]);
      }
    });
  });

  describe("updating data", () => {
    const updateLicensingEndpoints =
      (
        body: UpdateUserIndicator[] = [
          <UpdateUserIndicator>{
            name: "Sender",
            user_indicator: "",
          },
        ]
      ) =>
      ({ driver }: SetupFactoryOptions) => {
        driver.mockEndpoint(`${getDefaultConfig().service_control_url}licensing/endpoints/update`, {
          body,
          method: "post",
          status: 200,
        });
        return [];
      };

    const setup = async () => {
      const tableName = "Special table";

      const { driver } = await renderComponent({ source: DataSource.Broker, indicatorOptions: [UserIndicator.PlannedToDecommission, UserIndicator.NServiceBusEndpoint], ariaLabel: tableName }, async (driver) => {
        await driver.setUp(
          precondition.hasLicensingEndpoints([
            { name: `Not set yet`, is_known_endpoint: false, user_indicator: "", max_daily_throughput: 100 },
            { name: `Set and needs updating`, is_known_endpoint: false, user_indicator: UserIndicator.PlannedToDecommission, max_daily_throughput: 50 },
          ])
        );
        await driver.setUp(updateLicensingEndpoints());
      });

      const user = userEvent.setup();

      const table = screen.getByRole("table", { name: tableName });

      await driver.setUp(
        precondition.hasLicensingEndpoints([
          { name: `Not set yet`, is_known_endpoint: false, user_indicator: UserIndicator.NServiceBusEndpoint, max_daily_throughput: 100 },
          { name: `Set and needs updating`, is_known_endpoint: false, user_indicator: UserIndicator.NServiceBusEndpoint, max_daily_throughput: 50 },
        ])
      );

      return { user, table };
    };

    test("single", async () => {
      const { user, table } = await setup();

      for (const row of within(table).getAllByRole("row").slice(1)) {
        const dropdown = within(row).getByRole("combobox");
        // eslint-disable-next-line no-await-in-loop
        await user.selectOptions(dropdown, UserIndicator.NServiceBusEndpoint);
      }

      for (const row of within(table).getAllByRole("row").slice(1)) {
        const dropdown = within(row).getByRole("combobox") as HTMLSelectElement;
        expect(dropdown.value).toBe(UserIndicator.NServiceBusEndpoint);
      }
    });

    test("bulk", async () => {
      const { user, table } = await setup();

      const bulkDropdown = screen.getByRole("button", { name: /Set Endpoint Type for all items below/i });
      await user.click(bulkDropdown);
      const link = within(bulkDropdown.parentElement!).getByRole("link", { name: /NServiceBus Endpoint/i });
      await user.click(link);
      const confirmDialog = screen.getByRole("dialog", { name: /Proceed with bulk operation/i });
      const yesButton = within(confirmDialog).getByRole("button", { name: /Yes/i });
      await user.click(yesButton);

      for (const row of within(table).getAllByRole("row").slice(1)) {
        const dropdown = within(row).getByRole("combobox") as HTMLSelectElement;
        expect(dropdown.value).toBe(UserIndicator.NServiceBusEndpoint);
      }
    });
  });
});
