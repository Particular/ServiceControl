import { screen } from "@testing-library/vue";

export async function navigateToHeartbeatsConfiguration() {
  const configurationTab = await screen.findByRole("tab", { name: "Configuration" });
  configurationTab.click();

  // Wait for the tab to switch
  //TODO: determine why timeout had to be increased when these tests are run as part of the full application suite
  await screen.findByRole("region", { name: "Endpoint Configuration" }, { timeout: 5000 });
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
