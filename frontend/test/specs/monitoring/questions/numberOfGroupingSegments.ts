import { screen } from "@testing-library/vue";

export async function numberOfGroupingSegments() {
  return (await screen.findAllByRole("link", { name: /max\..*segments/i })).length;
}
