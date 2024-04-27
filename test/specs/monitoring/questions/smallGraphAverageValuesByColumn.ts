import { screen, within } from "@testing-library/vue";
import { columnName } from "@/components/monitoring/EndpointListRow.vue";
import { fireEvent } from "@testing-library/vue";

export async function smallGraphAverageValuesByColumn({ column }: { column: columnName | string }) {
  const ungroupedEndpoints = screen.getByRole("treeitem", { name: "ungrouped-endpoints" });
  const endpointRows = within(ungroupedEndpoints).getAllByRole("row");
  const averageValues: string[] = [];

  for (const row of endpointRows) {
    const gridCell = within(row).getByRole("gridcell", { name: column });
    const graphImage = within(gridCell).getByRole("image", { name: column });
    await fireEvent.mouseOver(graphImage);
    const averageValueElement = within(graphImage).getByRole("text", { name: "average-value" });

    const textContent = averageValueElement.textContent;
    const valueWithoutSuffix = textContent?.split(" ")[0];

    averageValues.push(valueWithoutSuffix || "");
  }

  return averageValues;
}
