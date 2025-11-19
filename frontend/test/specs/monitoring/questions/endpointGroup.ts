import { screen, within } from "@testing-library/vue";

export function endpointGroup(groupName: RegExp | string) {
  const allGroupedEndpoints = screen.getByRole("rowgroup", { name: "grouped-endpoints" });
  const groupOfEndpoints = within(allGroupedEndpoints).getByRole("group", { name: groupName });
  const endpoints = within(groupOfEndpoints).getAllByRole("link", { name: /details-link/i });
  const endpointNames = endpoints.map((item) => item.textContent);
  return {
    Endpoints: endpointNames,
  };
}
