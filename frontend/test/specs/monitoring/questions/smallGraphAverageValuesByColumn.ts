import { screen, within, fireEvent } from "@testing-library/vue";

export enum columnName {
  ENDPOINTNAME = "name",
  QUEUELENGTH = "queueLength",
  THROUGHPUT = "throughput",
  SCHEDULEDRETRIES = "retries",
  PROCESSINGTIME = "processingTime",
  CRITICALTIME = "criticalTime",
}
export async function smallGraphAverageValuesByColumn({ column }: { column: columnName | string }) {
  const ungroupedEndpoints = screen.getByRole("rowgroup", { name: "ungrouped-endpoints" });
  const endpointRows = within(ungroupedEndpoints).getAllByRole("row");
  const averageValues: string[] = [];

  for (const row of endpointRows) {
    const gridCell = within(row).getByRole("gridcell", { name: column });
    const graphImage = within(gridCell).getByRole("img", { name: column });
    // eslint-disable-next-line no-await-in-loop
    await fireEvent.mouseOver(graphImage);
    const averageValueElement = within(graphImage).getByRole("text", { name: "average-value" });

    const textContent = averageValueElement.textContent;
    const valueWithoutSuffix = textContent?.split(" ")[0];

    averageValues.push(valueWithoutSuffix || "");
  }

  return averageValues;
}
