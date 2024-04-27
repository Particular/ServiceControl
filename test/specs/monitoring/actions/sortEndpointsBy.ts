import { fireEvent } from "@testing-library/vue";
import { endpointSortingColumnWithName } from "../questions/endpointSortingColumnWithName";
import { columnName } from "@/components/monitoring/EndpointListRow.vue";

export async function sortEndpointsBy({ column }: { column: columnName | string }) {
  const filterRegEx = new RegExp(column, "i");
  await fireEvent.click(await endpointSortingColumnWithName(filterRegEx));
}
