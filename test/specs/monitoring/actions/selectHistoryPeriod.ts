import userEvent from "@testing-library/user-event";
import { screen, within } from "@testing-library/vue";

export async function selectHistoryPeriod(historyPeriod: number) {
  const historyPeriodList = await screen.findByRole("list", { name: "history-period-list" });
  const historyPeriodListItem = await within(historyPeriodList).findByRole("listitem", { name: `${historyPeriod}` });
  const historyPeriodLink = await within(historyPeriodListItem).findByRole("link");
  await userEvent.click(historyPeriodLink);
}
