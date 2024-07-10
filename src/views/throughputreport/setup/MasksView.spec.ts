import { test } from "../../../../test/drivers/vitest/driver";
import { userEvent } from "@component-test-utils";
import MasksView from "@/views/throughputreport/setup/MasksView.vue";
import { describe, expect } from "vitest";
import * as precondition from "../../../../test/preconditions";
import { useServiceControl } from "@/composables/serviceServiceControl";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import { minimumSCVersionForThroughput } from "@/views/throughputreport/isThroughputSupported";
import flushPromises from "flush-promises";
import { Driver } from "../../../../test/driver";
import Toast from "vue-toastification";
import { render, screen } from "@testing-library/vue";

describe("MaskView tests", () => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;

  async function setup(driver: Driver) {
    window.defaultConfig.monitoring_urls = ["!"];
    await driver.setUp(({ driver }) => precondition.hasServiceControlMainInstance({ driver }, minimumSCVersionForThroughput));
    await driver.setUp(precondition.hasUpToDateServicePulse);
    await driver.setUp(precondition.hasUpToDateServiceControl);
    await driver.setUp(precondition.hasNoErrors);
  }

  async function renderComponent(driver: Driver, body: string[] = []) {
    await setup(driver);
    driver.mockEndpoint(`${serviceControlInstanceUrl}licensing/settings/masks`, { body });
    useServiceControlUrls();
    await useServiceControl();
    const { debug } = render(MasksView, { global: { plugins: [Toast] } });
    await flushPromises();
    return { debug };
  }

  function getTextAreaElement() {
    return screen.getByRole("textbox", { name: /List of words to mask/i }) as HTMLTextAreaElement;
  }

  test("renders empty list", async ({ driver }) => {
    await renderComponent(driver);

    expect(getTextAreaElement().value).toBe("");
  });

  test("renders mask list loaded from server", async ({ driver }) => {
    await renderComponent(driver, ["first", "second"]);

    expect(getTextAreaElement().value).toBe("first\nsecond");
  });

  test("update mask list", async ({ driver }) => {
    await renderComponent(driver, ["first", "second"]);

    const use = userEvent.setup();
    await use.type(getTextAreaElement(), "\nthree\nfour\nfive");

    expect(getTextAreaElement().value).toBe("first\nsecond\nthree\nfour\nfive");
  });

  test("save mask list", async ({ driver }) => {
    await renderComponent(driver, ["first", "second"]);

    const use = userEvent.setup();
    await use.type(getTextAreaElement(), "\nthree\nfour\nfive");

    driver.mockEndpoint(`${serviceControlInstanceUrl}licensing/settings/masks/update`, { body: undefined, method: "post" });
    await use.click(screen.getByRole("button", { name: /Save/i }));

    expect(screen.queryAllByText(/Masks Saved/i).length).toBeGreaterThanOrEqual(1);
  });
});
