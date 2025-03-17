import { screen, within } from "@testing-library/vue";

// Assumes the current page is /heartbeats/instances/{instance name}
export async function getEndpointInstance(instanceName: string) {
  const endpointRow = await screen.findByRole("row", { name: instanceName });
  const hostName = within(endpointRow).getByLabelText("instance-name").textContent;
  const lastHeartbeat = within(endpointRow).getByTitle("Last Heartbeat").textContent;
  const muted = (<HTMLInputElement>within(endpointRow).getByRole("checkbox", { hidden: true })).checked;

  return {
    hostName,
    lastHeartbeat,
    muted,
    endpointRow,
  };
}
