import { screen, within } from "@testing-library/vue";

export async function queryHeartbeatMenuItem() {
  const navBar = await screen.findByRole("navigation");
  const menuItem = await within(navBar).getByRole("link", { name: "Heartbeats Menu Item" });
  const counter = within(menuItem).queryByLabelText("Alert Count");

  return {
    menuItemName: menuItem.ariaLabel,
    isCounterVisible: counter && true,
    counterValue: counter ? Number(counter.textContent) : 0,
  };
}
