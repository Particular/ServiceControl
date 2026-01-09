import { screen, within } from "@testing-library/vue";
import UserEvent from "@testing-library/user-event";
export async function getEditAndRetryEditor() {
  const dialog = await screen.findByRole("dialog", { name: "edit and retry message" });

  return {
    async switchToMessageBodyTab() {
      const tab = within(dialog).getByRole("tab", { name: /message body/i });
      await UserEvent.click(tab);
    },

    bodyFieldIsReadOnly() {
      const textbox = within(within(dialog).getByLabelText("message body")).getByRole("textbox");
      return textbox.ariaReadOnly === "true";
    },

    hasWarningMatchingText(legendText: RegExp) {
      const information = within(dialog).getByRole("status", { name: /cannot edit message body/i });
      return within(information).getByText(legendText) !== null;
    },
  };
}
