import { screen } from "@testing-library/vue";

export async function navigateToHeartbeatsConfiguration() {
  const configurationTab = await screen.findByRole("tab", { name: "Configuration" });
  configurationTab.click();

  // Wait for the tab to switch
  await screen.findByRole("region", { name: "Endpoint Configuration" });
}

export async function navigateToHealthyHeartbeats() {
  const healthyHeartbeatsTab = await screen.findByRole("tab", { name: /Healthy Endpoints \(\d+\)/i });
  healthyHeartbeatsTab.click();

  // Wait for the tab to switch
  await screen.findByRole("region", { name: "Healthy Endpoints" });
}

export async function navigateToUnHealthyHeartbeats() {
  const unhealthyHeartbeatsTab = await screen.findByRole("tab", { name: /Unhealthy Endpoints \(\d+\)/i });
  unhealthyHeartbeatsTab.click();

  // Wait for the tab to switch
  await screen.findByRole("region", { name: "Unhealthy Endpoints" });
}
