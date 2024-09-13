import { screen, within } from "@testing-library/vue";

export async function getAlertNotifications() {
  const alerts = await screen.findAllByRole("alert");
  return alerts.map(function (alert) {
    return {
      textMatches: (match: RegExp) => within(alert).queryByText(match) !== null,
      hasLink({ caption, address }: { caption: string; address: string }) {
        return within(alert).getByRole("link", { name: caption }).getAttribute("href") === address;
      },
    };
  });
}
