import { screen } from "@testing-library/vue";

export async function endpointSortingColumnWithName(optionText: RegExp | string) {
  const filterRegEx = new RegExp(optionText, "i");
  return await screen.findByRole("button", { name: filterRegEx });
}
