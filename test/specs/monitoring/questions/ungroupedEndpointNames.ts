import { screen, within } from "@testing-library/vue";

export function ungroupedEndpointNames() {
  const ungroupedEndpoints = screen.getByRole("treeitem", { name: "ungrouped-endpoints" });
  const endpointRows = within(ungroupedEndpoints).getAllByRole("row");
  const endpointNames = endpointRows.map((item) => item.getAttribute("aria-labelledby") || item.getAttribute("aria-label"));
  return {
    Endpoints: endpointNames,
  };
}
