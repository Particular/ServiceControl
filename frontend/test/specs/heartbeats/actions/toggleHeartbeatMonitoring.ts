import { screen, within } from "@testing-library/vue";

export async function toggleHeartbeatMonitoring(instanceName: string) {
  const endpointRow = await screen.findByRole("row", { name: instanceName });

  (<HTMLInputElement>within(endpointRow).getByRole("checkbox", { hidden: true })).click();
}
