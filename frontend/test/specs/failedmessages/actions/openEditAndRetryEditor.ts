import { screen } from "@testing-library/vue";
import UserEvent from "@testing-library/user-event";
export async function openEditAndRetryEditor() {
  const button = await screen.findByRole("button", { name: "Edit & retry" });
  await UserEvent.click(button);
}
