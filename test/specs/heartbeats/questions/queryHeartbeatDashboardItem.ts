import { getDashboardItems } from "./getDashboardItems";

export async function queryHeartbeatDashboardItem() {
  const dashboardItems = await getDashboardItems();
  return dashboardItems ? dashboardItems.get("Heartbeats") : null;
}
