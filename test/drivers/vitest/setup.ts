import { afterAll, afterEach, beforeAll, beforeEach, vi } from "vitest";
import { mockServer } from "../../mock-server";
import "@testing-library/jest-dom/vitest";

const defaultConfig = {
  default_route: "/dashboard",
  base_url: "/",
  version: "1.2.0",
  service_control_url: "http://localhost:33333/api/",
  monitoring_urls: ["http://localhost:33633/"],
  showPendingRetry: false,
};

vi.stubGlobal("defaultConfig", defaultConfig);

beforeEach(() => {
  vi.stubGlobal("defaultConfig", defaultConfig);
});

beforeAll(() => {
  mockServer.listen({
    onUnhandledRequest: (request) => {
      console.log("Unhandled %s %s", request.method, request.url);
    },
  });
});

afterAll(async () => {
  //Intentionally not calling mockServer.close here to prevent ServicePulse in flight messages after app unmount to fail
});

function deleteAllCookies() {
  const cookies = document.cookie.split(";");

  for (let i = 0; i < cookies.length; i++) {
    const cookie = cookies[i];
    const eqPos = cookie.indexOf("=");
    const name = eqPos > -1 ? cookie.substr(0, eqPos) : cookie;
    document.cookie = name + "=;expires=Thu, 01 Jan 1970 00:00:00 GMT";
  }
}

afterEach(() => {
  //Intentionally not calling mockServer.resetHandlers here to prevent ServicePulse in flight messages after app unmount to fail.
  //mockServer.resetHandlers is being called instead in driver.ts

  //Make JSDOM create a fresh document per each test run
  jsdom.reconfigure({ url: "http://localhost:3000/" });
  localStorage.clear();
  sessionStorage.clear();
  deleteAllCookies();
});
