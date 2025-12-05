import { screen, within } from "@testing-library/vue";

export interface DashboardItemData {
  name: string;
  isCounterVisible: boolean;
  counterValue: number;
  dashboardItem: HTMLElement;
}

export async function getDashboardItems() {
  const dashboardItems = await screen.findAllByRole("link", { name: "Dashboard Item" });

  return new Map<string, DashboardItemData>(
    dashboardItems.map((di) => {
      const name = String(within(di).getByRole("heading").textContent);
      const counterElement = within(di).queryByLabelText("Alert Count");

      return [
        name,
        {
          name: name,
          isCounterVisible: counterElement !== null,
          counterValue: counterElement ? Number(counterElement.textContent) : 0,
          dashboardItem: di,
        },
      ];
    })
  );
}
