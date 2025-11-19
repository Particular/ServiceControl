import { screen, within } from "@testing-library/vue";

export function findSortImageInColumn(columnName: string, direction: "up" | "down") {
  const columnButton = screen.getByRole("columnheader", { name: columnName });
  const imageRole = direction === "up" ? "sort-up" : "sort-down";
  return within(columnButton).queryByRole("img", { name: imageRole });
}
