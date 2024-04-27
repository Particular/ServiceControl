import { fireEvent, screen } from "@testing-library/vue";
import { groupingOptionWithName } from "../questions/groupingOptionWithName";
import { isConstructorDeclaration } from "typescript";
import { openGroupingOptions } from "./openGroupingOptions";

export async function groupEndpointsBy({ numberOfSegments }: { numberOfSegments: number }) {
  await openGroupingOptions();
  const filterRegEx = numberOfSegments === 0 ? new RegExp(`no grouping`, "i") : new RegExp(`max\\. ${numberOfSegments} segments`, "i");
  await fireEvent.click(await groupingOptionWithName(filterRegEx));
}
