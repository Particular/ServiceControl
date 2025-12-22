import { screen, within } from "@testing-library/vue";

export async function endpointStaleWarning() {
  const staleWarning = await screen.findByRole("status", { name: "stale-warning" });
  return staleWarning;
}

export async function negativeCriticalTimeWarning() {
  const criticalTimeWarning = await screen.queryByRole("status", { name: "negative-critical-time-warning" });
  return criticalTimeWarning;
}

export async function endpointDisconnectedWarning() {
  const disconnectedWarning = await screen.findByRole("status", { name: "disconnected-warning" });
  return disconnectedWarning;
}

export async function endpointErrorCountWarning() {
  const errorCountWarning = await screen.findByRole("status", { name: "error-count-warning" });
  return errorCountWarning;
}

export async function endpointErrorCount() {
  const errorCountWarning = await screen.findByRole("status", { name: "error-count-warning" });
  const errorCount = await within(errorCountWarning).findByLabelText("error-count");
  return errorCount.textContent;
}
