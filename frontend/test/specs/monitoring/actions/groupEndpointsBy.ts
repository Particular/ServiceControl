import UserEvent from "@testing-library/user-event";
import { groupingOptionWithName } from "../questions/groupingOptionWithName";
import { openGroupingOptions } from "./openGroupingOptions";

export async function groupEndpointsBy({ numberOfSegments }: { numberOfSegments: number }) {
  await openGroupingOptions();
  const filterRegEx = numberOfSegments === 0 ? new RegExp(`no grouping`, "i") : new RegExp(`max\\. ${numberOfSegments} segments`, "i");
  await UserEvent.click(await groupingOptionWithName(filterRegEx));
}
