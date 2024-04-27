import { makeMockEndpoint } from "./mock-endpoint";
import userEvent from "@testing-library/user-event";

import { mockServer } from "./mock-server";

export { render, screen } from "@testing-library/vue";
export { expect, it, describe } from "vitest";
export { userEvent };

export const mockEndpoint = makeMockEndpoint({ mockServer });
