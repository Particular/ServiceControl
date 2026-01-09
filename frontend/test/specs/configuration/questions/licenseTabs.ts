import { screen } from "@testing-library/vue";

export async function licenseTabList() {
  const tabs = await screen.findAllByRole("tab");
  return tabs;
}
export async function licenseTabNames() {
  const tabs = await licenseTabList();
  // Check the names of the tabs
  const tabNames = tabs.map((tab) => tab.textContent?.trim());
  return tabNames;
}
