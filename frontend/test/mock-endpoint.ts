import { DefaultBodyType, http, HttpResponse, StrictRequest, type PathParams } from "msw";
import type { SetupWorker } from "msw/browser";
import { SetupServer } from "msw/node";
import { MockEndpointDynamicOptions, MockEndpointOptions, Method } from "./driver";

export const makeMockEndpoint =
  ({ mockServer }: { mockServer: SetupServer | SetupWorker }) =>
  (endpoint: string, { body, method = "get", status = 200, headers = {} }: MockEndpointOptions) => {
    mockServer.use(http[method](endpoint, () => HttpResponse.json(body, { status: status, headers: headers })));
  };

export const makeMockEndpointDynamic =
  ({ mockServer }: { mockServer: SetupServer | SetupWorker }) =>
  (endpoint: string, method: Method = "get", callBack: (url: URL, params: PathParams, request: StrictRequest<DefaultBodyType>) => Promise<MockEndpointDynamicOptions>) => {
    mockServer.use(
      http[method](endpoint, async ({ request, params }) => {
        const url = new URL(request.url.toString());
        const { body, status = 200, headers = {} } = await callBack(url, params, request);
        return HttpResponse.json(body, { status: status, headers: headers });
      })
    );
  };
