import { it as itVitest, describe } from "vitest";
import { Driver } from "../../driver";
import { mount } from "../../../src/mount";
import makeRouter from "../../../src/router";
import { mockEndpoint, mockEndpointDynamic } from "../../utils";
import { mockServer } from "../../mock-server";
import { App } from "vue";

function makeDriver() {
  let app: App<Element>;
  const driver = <Driver>{
    async goTo(path) {
      const router = makeRouter();
      try {
        await router.push(path);
      } catch (error) {
        // Ignore redirection error.
        if (error instanceof Error && error.message.includes("Redirected when going from")) {
          return;
        }

        throw error;
      }

      document.body.innerHTML = '<div id="app"></div><div id="modalDisplay"></div>';
      app = mount({ router });
    },
    mockEndpoint,
    mockEndpointDynamic: mockEndpointDynamic,
    setUp(factory) {
      return factory({ driver: this });
    },
    disposeApp() {
      if (!app) {
        throw new Error("App is not mounted, make your sure to await driver.goTo().");
      }
      app.unmount();
    },
  };
  return driver;
}

const test = itVitest.extend<{ driver: Driver }>({
  driver: async ({}, use: any) => {
    //Reset the mocked handlers before executing the test
    mockServer.resetHandlers();

    const driver = makeDriver();
    //run the test
    await use(driver);

    //unmount the app after the test runs
    driver.disposeApp();
  },
});

export { test, describe };
