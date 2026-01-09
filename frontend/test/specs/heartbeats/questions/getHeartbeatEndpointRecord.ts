import { screen, within, waitFor } from "@testing-library/vue";

export type EndpointRecord = {
  name: string;
  instanceCount: string;
  lastHeartbeat: string;
  trackInstances: boolean;
  instancesMuted: number;
};

export async function getHeartbeatEndpointRecord(endpointName: string) {
  const endpointRow = await waitFor(() => screen.queryByRole("row", { name: endpointName }));

  if (!endpointRow) {
    return null;
  }
  return createRecordFromRowElement(endpointRow);
}

export async function getAllHeartbeatEndpointRecords() {
  const endpointRowgroup = await screen.findByRole("rowgroup", { name: "endpoints" });
  const endpointRows = within(endpointRowgroup).queryAllByRole("row");
  return endpointRows ? endpointRows.map((row) => createRecordFromRowElement(row)) : [];
}

function createRecordFromRowElement(endpointRow: HTMLElement) {
  const mutedInstanceCountElement = within(endpointRow).queryByLabelText("Muted instance count");

  return <EndpointRecord>{
    name: within(endpointRow).getByRole("link", { name: "details-link" }).textContent,
    instanceCount: within(endpointRow).getByLabelText("instance-count").textContent,
    lastHeartbeat: within(endpointRow).getByTitle("Last Heartbeat").textContent,
    trackInstances: within(endpointRow).queryByTitle("Instances are being tracked") != null,
    instancesMuted: mutedInstanceCountElement === null ? 0 : Number(mutedInstanceCountElement.textContent),
  };
}
