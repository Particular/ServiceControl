import { setupWorker } from "msw/browser";
import { Driver } from "../driver";
import { makeMockEndpoint } from "../mock-endpoint";
import * as precondition from "../preconditions";
export const worker = setupWorker();
const mockEndpoint = makeMockEndpoint({ mockServer: worker });

const makeDriver = (): Driver => ({
  async goTo() {
    throw new Error("Not implemented");
  },
  mockEndpoint,
  setUp(factory) {
    return factory({ driver: this });
  },
  disposeApp() {
    throw new Error("Not implemented");
  },
});

const driver = makeDriver();

(async () => {
  await driver.setUp(precondition.serviceControlWithMonitoring);
  //override the default mocked endpoints with a custom list
  await driver.setUp(
    precondition.monitoredEndpointsNamed([
      "Universe.Solarsystem.Mercury.Endpoint1",
      "Universe.Solarsystem.Mercury.Endpoint2",
      "Universe.Solarsystem.Venus.Endpoint3",
      "Universe.Solarsystem.Venus.Endpoint4",
      "Universe.Solarsystem.Earth.Endpoint5",
      "Universe.Solarsystem.Earth.Endpoint6",
    ])
  );
})();
