import { screen, within } from "@testing-library/vue";

export function groupingOptions() {
  const list = screen.getByRole("list", { name: /Group by:/i });
  return within(list).getAllByRole("listitem");
}
