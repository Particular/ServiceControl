import { screen, within } from "@testing-library/vue";

export async function getTrialBar() {
  const trialBar = await screen.findByRole("status", { name: /trial license bar information/i });
  return {
    textMatches: (match: RegExp) => within(trialBar).queryByText(match) !== null,
    hasLinkWithCaption(caption: string) {
      return {
        address: within(trialBar).getByRole("link", { name: caption }).getAttribute("href"),
      };
    },
  };
}
