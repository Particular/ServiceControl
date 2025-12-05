import { screen } from "@testing-library/vue";

export function paginationVisible() {
  const pagination = screen.queryByRole("list", { name: /pagination/i });
  return pagination ? true : false;
}
