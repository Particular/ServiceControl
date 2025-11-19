import { flushPromises } from "@vue/test-utils";
import { test as testVitest, describe } from "vitest";
import { Driver } from "../../driver";
import { mount } from "@/mount";
import makeRouter from "../../../src/router";
import { mockEndpoint, mockEndpointDynamic } from "../../utils";
import { App } from "vue";
import { mockServer } from "../../mock-server";

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
      app.unmount();
    },
  };
  return driver;
}

function deleteAllCookies() {
  const cookies = document.cookie.split(";");

  for (let i = 0; i < cookies.length; i++) {
    const cookie = cookies[i];
    const eqPos = cookie.indexOf("=");
    const name = eqPos > -1 ? cookie.slice(0, eqPos) : cookie;
    document.cookie = name + "=;expires=Thu, 01 Jan 1970 00:00:00 GMT";
  }
}

const test = testVitest.extend<{ driver: Driver }>({
  // eslint-disable-next-line no-empty-pattern, @typescript-eslint/no-explicit-any
  driver: async ({}, use: any) => {
    const driver = makeDriver();
    console.log("Starting test");

    //run the test
    await use(driver);

    console.log("Test ended");
    //unmount the app after the test runs
    driver.disposeApp();

    // We need to wait for any pending promises to resolve before resetting handlers and clearing storage
    await flushPromises();

    console.log("Cleanup after test");
    mockServer.resetHandlers();
    //Make JSDOM create a fresh document per each test run
    jsdom.reconfigure({ url: "http://localhost:3000/" });
    localStorage.clear();
    sessionStorage.clear();
    deleteAllCookies();
  },
});

export { test, describe };
