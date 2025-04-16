import { makeMockEndpoint, makeMockEndpointDynamic } from "./mock-endpoint";
import userEvent from "@testing-library/user-event";

import { mockServer } from "./mock-server";
import { Driver } from "./driver";

export { render, screen, within } from "@testing-library/vue";
export { expect, test, describe } from "vitest";
export { userEvent };

export const mockEndpoint = makeMockEndpoint({ mockServer });
export const mockEndpointDynamic = makeMockEndpointDynamic({ mockServer });

export function makeDriverForTests(): Driver {
  return {
    goTo() {
      throw new Error("Not implemented");
    },
    mockEndpoint,
    mockEndpointDynamic,
    setUp(factory) {
      return factory({ driver: this });
    },
    disposeApp() {
      throw new Error("Not implemented");
    },
  };
}
