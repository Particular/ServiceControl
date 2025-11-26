import { makeDriverForTests, userEvent, render, screen } from "@component-test-utils";
import MasksView from "@/views/throughputreport/setup/MasksView.vue";
import { describe, expect, test } from "vitest";
import * as precondition from "../../../../test/preconditions";
import { minimumSCVersionForThroughput } from "@/views/throughputreport/isThroughputSupported";
import Toast from "vue-toastification";
import { disableMonitoring } from "../../../../test/drivers/vitest/setup";
import { flushPromises } from "@vue/test-utils";
import { createTestingPinia } from "@pinia/testing";
import { useServiceControlStore } from "@/stores/ServiceControlStore";
import { setActivePinia } from "pinia";
import { getDefaultConfig } from "@/defaultConfig";

describe("MaskView tests", () => {
  async function setup() {
    const driver = makeDriverForTests();

    disableMonitoring();

    await driver.setUp(precondition.hasServiceControlMainInstance(minimumSCVersionForThroughput));
    await driver.setUp(precondition.hasUpToDateServicePulse);
    await driver.setUp(precondition.hasUpToDateServiceControl);
    await driver.setUp(precondition.errorsDefaultHandler);

    return driver;
  }

  async function renderComponent(body: string[] = []) {
    const driver = await setup();
    driver.mockEndpoint(`${getDefaultConfig().service_control_url}licensing/settings/masks`, { body });
    setActivePinia(createTestingPinia({ stubActions: false }));

    useServiceControlStore();

    const { debug } = render(MasksView, { global: { plugins: [Toast], directives: { tippy: () => {} } } });
    await flushPromises();

    return { debug, driver };
  }

  function getTextAreaElement() {
    return screen.getByRole("textbox", { name: /List of words to mask/i }) as HTMLTextAreaElement;
  }

  test("renders empty list", async () => {
    await renderComponent();

    expect(getTextAreaElement().value).toBe("");
  });

  test("renders mask list loaded from server", async () => {
    await renderComponent(["first", "second"]);

    expect(getTextAreaElement().value).toBe("first\nsecond");
  });

  test("update mask list", async () => {
    await renderComponent(["first", "second"]);

    const use = userEvent.setup();
    await use.type(getTextAreaElement(), "\nthree\nfour\nfive");

    expect(getTextAreaElement().value).toBe("first\nsecond\nthree\nfour\nfive");
  });

  test("save mask list", async () => {
    const { driver } = await renderComponent(["first", "second"]);

    const use = userEvent.setup();
    await use.type(getTextAreaElement(), "\nthree\nfour\nfive");

    driver.mockEndpoint(`${getDefaultConfig().service_control_url}licensing/settings/masks/update`, { body: undefined, method: "post" });
    await use.click(screen.getByRole("button", { name: /Save/i }));

    expect(screen.queryAllByText(/Masks Saved/i).length).toBeGreaterThanOrEqual(1);
  });
});
