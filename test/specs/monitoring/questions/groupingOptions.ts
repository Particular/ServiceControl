import { screen, within } from "@testing-library/vue";

export function groupingOptions() {
  const list = screen.getByRole("list", { name: /Group by:/i });
  const { getAllByRole } = within(list);
  const items = getAllByRole("listitem");
  return items;
}
