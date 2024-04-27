import { it as itVitest, describe } from "vitest";
import { Driver } from "../../driver";
import { mount } from "../../../src/mount";
import makeRouter from "../../../src/router";
import { mockEndpoint } from "../../utils";

const makeDriver = (): Driver => ({
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

    document.body.innerHTML = '<div id="app"></div>';
    mount({ router });
  },
  mockEndpoint,
  setUp(factory) {
    return factory({ driver: this });
  },
});

const it = itVitest.extend<{ driver: Driver }>({
  driver: async ({}, use: any) => {
    await use(makeDriver());
  },
});

export { it, describe };
