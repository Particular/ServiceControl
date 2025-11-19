import { screen } from "@testing-library/vue";

export async function getNothingToConfigureStatus() {
  return await screen.findByRole("status");
}
