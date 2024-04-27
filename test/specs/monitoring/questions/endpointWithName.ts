import { screen } from "@testing-library/vue";

export function endpointWithName(endpointName: RegExp | string) {
  return screen.queryByRole("link", { name: endpointName });
}
