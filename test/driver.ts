import { DefaultBodyType, StrictRequest, type PathParams } from "msw";

type GoTo = (path: string) => Promise<void>;
type DisposeApp = () => void;

export type Method = "get" | "post" | "patch" | "put" | "delete" | "options";

export type MockEndpointOptions = {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  body: Record<string, any> | string | number | boolean | null | undefined;
  method?: Method;
  status?: number;
  headers?: { [key: string]: string };
};

export type MockEndpointDynamicOptions = {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  body: Record<string, any> | string | number | boolean | null | undefined;
  status?: number;
  headers?: { [key: string]: string };
};

type MockEndpoint = (path: string, options: MockEndpointOptions) => void;
type MockEndpointDynamic = (endpoint: string, method: Method, callBack: (url: URL, params: PathParams, request: StrictRequest<DefaultBodyType>) => Promise<MockEndpointDynamicOptions>) => void;

export type SetupFactoryOptions = {
  driver: Driver;
};
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type SetupFactory = ({ driver }: SetupFactoryOptions) => any;

type SetUp = <Factory extends SetupFactory>(factory: Factory) => Promise<ReturnType<Factory>>;

export type Driver = {
  goTo: GoTo;
  mockEndpoint: MockEndpoint;
  mockEndpointDynamic: MockEndpointDynamic;
  setUp: SetUp;
  disposeApp: DisposeApp;
};
