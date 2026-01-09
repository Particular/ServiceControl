import userEvent from "@testing-library/user-event";
import { screen, within } from "@testing-library/vue";
import { vi } from "vitest";

export async function selectHistoryPeriod(historyPeriod: number, advanceTimers: boolean = false) {
  const historyPeriodList = await screen.findByRole("list", { name: "history-period-list" });
  const historyPeriodListItem = await within(historyPeriodList).findByRole("listitem", { name: `${historyPeriod}` });
  const historyPeriodLink = await within(historyPeriodListItem).findByRole("link");
  if (advanceTimers) {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await user.click(historyPeriodLink);
  } else {
    await userEvent.click(historyPeriodLink);
  }
}
