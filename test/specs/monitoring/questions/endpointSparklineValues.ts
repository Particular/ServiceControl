import { screen, within } from "@testing-library/vue";

export async function endpointSparklineValues(endpointName: string) {
  const endpointRow = await screen.findByRole("row", { name: endpointName });
  const endpointSparklineElement = within(endpointRow).getAllByRole("text", { name: /sparkline/i });
  return endpointSparklineElement.map((item) => item.textContent?.split(" ")[0]);
}
