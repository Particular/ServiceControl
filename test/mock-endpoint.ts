import { http, HttpResponse } from "msw";
import type { SetupWorker } from "msw/browser";
import { SetupServer } from "msw/node";
import { MockEndpointOptions } from "./driver";

export const makeMockEndpoint =
  ({ mockServer }: { mockServer: SetupServer | SetupWorker }) =>
  (
    endpoint: string,
    {
      body,
      method = "get",
      status = 200,
      headers = {},
    }: MockEndpointOptions
  ) => {
    mockServer.use(http[method](endpoint, () => HttpResponse.json(body, { status: status, headers: headers })));
  };
