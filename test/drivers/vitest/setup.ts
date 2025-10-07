import { afterAll, beforeAll, beforeEach, vi } from "vitest";
import { mockServer } from "../../mock-server";
import "@testing-library/jest-dom/vitest";

const defaultConfig = {
  default_route: "/dashboard",
  version: "1.2.0",
  service_control_url: "http://localhost:33333/api/",
  monitoring_urls: ["http://localhost:33633/"],
  showPendingRetry: false,
};

export function disableMonitoring() {
  vi.stubGlobal("defaultConfig", { ...defaultConfig, ...{ monitoring_urls: ["!"] } });
}

beforeEach(() => {
  vi.stubGlobal("defaultConfig", defaultConfig);
});

beforeAll(() => {
  console.log("Starting mock server");

  mockServer.listen({
    onUnhandledRequest: (request, { error }) => {
      console.log("Unhandled %s %s", request.method, request.url);
      error();
    },
  });
});

afterAll(() => {
  console.log("Shutting down mock server");
  mockServer.close();
});
