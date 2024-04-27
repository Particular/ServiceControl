import { http } from "msw";
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
      body: string | unknown[] | Record<string | number, unknown>;
      method?: "get" | "post" | "put" | "patch" | "delete";
      status?: number;
      headers?: { [key: string]: string };
    }
  ) => {
    mockServer.use(
      http[method](endpoint, () => {
        return new Response(JSON.stringify(body), {
          status: status,
          headers: {
            ...headers,
            "Content-Type": "application/json",
          },
        });
      })
    );
  };
