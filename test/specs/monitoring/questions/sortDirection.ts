import { screen, within } from "@testing-library/vue";

export function findSortImageInColumn(columnName: string, direction: "up" | "down") {
  const columnButton = screen.getByRole("columnheader", { name: columnName });
  const { queryByRole } = within(columnButton);
  const imageRole = direction === "up" ? "sort-up" : "sort-down";
  const sortImage = queryByRole("image", { name: imageRole });

  return sortImage;
}
