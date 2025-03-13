import { screen, within } from "@testing-library/vue";
import UserEvent from "@testing-library/user-event";
export async function getEditAndRetryEditor() {
  const dialog = await screen.findByRole("dialog", { name: "edit and retry message" });

  return {
    async switchToMessageBodyTab() {
      const tab = within(dialog).getByRole("tab", { name: /message body/i });
      await UserEvent.click(tab);
    },

    bodyFieldIsDisabled() {
      return within(dialog).getByRole("textbox", { name: "message body" }).hasAttribute("readonly");
    },

    hasWarningMatchingText(legendText: RegExp) {
      const information = within(dialog).getByRole("status", { name: /cannot edit message body/i });
      return within(information).getByText(legendText) !== null;
    },
  };
}
