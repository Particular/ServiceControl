import { screen } from "@testing-library/vue";

export async function getHeartbeatFilterValue() {
  const filterBox = <HTMLInputElement>await screen.findByRole("searchbox", { name: "Filter by name" });

  return filterBox.value;
}
