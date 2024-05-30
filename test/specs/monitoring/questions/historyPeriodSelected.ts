import { screen, within } from "@testing-library/vue";

export async function historyPeriodSelected(historyPeriod: number) {
  const historyPeriodList = await screen.findByRole("list", { name: "history-period-list" });
  const historyPeriodListItem = await within(historyPeriodList).findByRole("listitem", { name: `${historyPeriod}` });
  return historyPeriodListItem.getAttribute("aria-selected");
}
