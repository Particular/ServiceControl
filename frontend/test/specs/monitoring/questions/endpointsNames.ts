import { screen, within } from "@testing-library/vue";

export async function endpointsNames() {
  const endpointList = await screen.findByRole("table", { name: "endpoint-list" });
  const endpoints = within(endpointList).queryAllByRole("link", { name: /details-link/i });
  return endpoints.map((item) => item.textContent);
}
