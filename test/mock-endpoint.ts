import { http, HttpResponse } from "msw";
import type { SetupWorker } from "msw/browser";
import { SetupServer } from "msw/node";

export const makeMockEndpoint =
  ({ mockServer }: { mockServer: SetupServer | SetupWorker }) =>
  (
    endpoint: string,
    {
      body,
      method = "get",
      status = 200,
      headers = {},
    }: {
      body: Record<string, any> | string | number | boolean | null | undefined;
      method?: "get" | "post" | "put" | "patch" | "delete";
      status?: number;
      headers?: { [key: string]: string };
    }
  ) => {
    mockServer.use(http[method](endpoint, () => HttpResponse.json(body, { status: status })));
  };
