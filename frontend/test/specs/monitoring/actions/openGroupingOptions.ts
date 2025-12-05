import userEvent from "@testing-library/user-event";
import { screen } from "@testing-library/vue";

export async function openGroupingOptions() {
  await userEvent.click(await screen.findByRole("button", { name: /group-by-btn/i }));
}
