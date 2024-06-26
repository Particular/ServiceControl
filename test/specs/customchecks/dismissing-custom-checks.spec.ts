import { it, describe } from "../../drivers/vitest/driver";

describe("FEATURE: Dismiss custom checks", () => {
  describe("RULE: Dismiss button should be visible", () => {
    it.todo("EXAMPLE: Dismiss button should be visible on each failing custom check");

    /* SCENARIO
          Given 2 failing custom checks
          And the custom checks page is open
          Then each should render a dismiss button
        */
  });
  describe("RULE: Dismissing a custom check should remove from the list", () => {
    it.todo("EXAMPLE: The dismiss button should remove the custom check from the list when clicked");

    /* SCENARIO
          Given 2 failing custom checks
          When the dismiss button is clicked
          Then the dismissed custom check should be removed from the list
        */
  });
  describe("RULE: Failing after a dismiss should cause the failed check to reappear", () => {
    it.todo("EXAMPLE: Dismissed custom check should reappear in the list when it fails");

    /* SCENARIO
          Given 2 failed custom checks
          And one of them is dismissed
          When the dismissed custom check fails
          Then the custom check should appear in the list
        */
  });
});
