import { makeMockEndpoint, makeMockEndpointDynamic } from "./mock-endpoint";
import userEvent from "@testing-library/user-event";

import { mockServer } from "./mock-server";

export { render, screen } from "@testing-library/vue";
export { expect, test, describe } from "vitest";
export { userEvent };

export const mockEndpoint = makeMockEndpoint({ mockServer });
export const mockEndpointDynamic = makeMockEndpointDynamic({ mockServer });

