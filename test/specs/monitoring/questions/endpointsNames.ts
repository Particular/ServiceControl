import { screen, within } from "@testing-library/vue";

export async function endpointsNames() {
  const group = await screen.findByRole("treegrid", {name:"endpoint-list"});
  const { queryAllByRole } = within(group);
  const items = queryAllByRole("row");
  const endpointNames = items.map((item) => item.getAttribute("aria-labelledby") || item.getAttribute("aria-label"));
  return endpointNames;
}
