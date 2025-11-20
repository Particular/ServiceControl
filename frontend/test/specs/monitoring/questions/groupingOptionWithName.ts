import { screen } from "@testing-library/vue";

export async function groupingOptionWithName(optionText: RegExp | string) {
  return await screen.findByRole("link", { name: optionText });
}
