import { screen, within } from "@testing-library/vue";

export async function endpointDetailsGraphsCurrentValues() {
  const graphDetails = await screen.getByRole("grid", { name: "detail-graphs-data" });
  const allGraphCurrentValues = within(graphDetails).getAllByLabelText("metric-current-value");

  const data = allGraphCurrentValues.map((element) => {
    return element.textContent?.split(" ")[0] || null;
  });

  return data;
}
