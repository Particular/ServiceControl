import { afterAll, beforeAll, beforeEach } from "vitest";
import { mockServer } from "../../mock-server";
import "@testing-library/jest-dom/vitest";
import { setDefaultConfig, type DefaultConfig } from "@/defaultConfig";

const defaultConfig: DefaultConfig = {
  default_route: "/dashboard",
  version: "1.2.0",
  service_control_url: "http://localhost:33333/api/",
  monitoring_url: "http://localhost:33633/",
  showPendingRetry: false,
};

export function disableMonitoring() {
  setDefaultConfig({ ...defaultConfig, monitoring_url: "!" });
}

beforeEach(() => {
  setDefaultConfig(defaultConfig);
});

beforeAll(() => {
  console.log("Starting mock server");

  mockServer.listen({
    onUnhandledRequest: (_, print) => {
      print.warning();
    },
  });
});

afterAll(() => {
  console.log("Shutting down mock server");
  mockServer.close();
});
