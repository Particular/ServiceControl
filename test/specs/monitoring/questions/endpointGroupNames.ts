import { screen } from "@testing-library/vue";

export function endpointGroupNames() {
  const groups = screen.queryAllByRole("group");
  return groups.map((group) => group.getAttribute("aria-labelledby") || group.getAttribute("aria-label"));
}
