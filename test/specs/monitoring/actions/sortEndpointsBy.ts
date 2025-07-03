import UserEvent from "@testing-library/user-event";
import { endpointSortingColumnWithName } from "../questions/endpointSortingColumnWithName";

export async function sortEndpointsBy({ column }: { column: string }) {
  const filterRegEx = new RegExp(column, "i");
  await UserEvent.click(await endpointSortingColumnWithName(filterRegEx));
}
