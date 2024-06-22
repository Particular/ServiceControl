import { it, describe } from "../../drivers/vitest/driver";

describe("FEATURE: Failing custom checks", () => {
  describe("Rule: Custom checks are displayed", () => {
    it.todo("Example: All custom checks are in a failed state and should be displayed in a list on the custom checks tab");

    /* SCENARIO
          Given there are custom checks
          And all custom checks are in a failed state
          When navigating to the custom checks tab
          Then a list of custom checks is shown
        */

    /* NOTES
          Failure reason (if given)
          Name (Check)
          Category
          Endpoint
          Host
          Last checked
        */
  });
  describe("Rule: Only failed custom checks are displayed", () => {
    it.todo("Example: Only failed custom checks should be displayed in the list on the custom checks tab");

    /* SCENARIO
          Given there are two failed custom checks
          And one passing custom check
          When navigating to the custom checks tab
          Then the two failed custom checks are shown
          And the passing custom check is not shown
        */
  });
  describe("Rule: Failed custom checks should be shown in descending order of last checked", () => {
    it.todo("Example: Three failed custom checks should be displayed in descending order of last checked on the custom checks tab");

    /* SCENARIO
          Given there are three failed custom checks
          And the custom checks failed at different times
          When navigating to the custom checks tab
          Then the custom checks are shown in descending order of last checked
        */
  });
  describe("Rule: Failed custom checks should have pagination", () => {
    it.todo("Example: 51 failed custom checks should be paginated on the custom checks tab");

    /* SCENARIO
          Given there are 51 failed custom checks
          When navigating to the custom checks tab
          Then the pagination controls should be visible
          And the page number should be 1
          And only the first 50 custom checks should be rendered
          And page 2 should be available to click on
        */

    it.todo("Example: 49 failed custom checks should not be paginated on the custom checks tab");
    /* SCENARIO
          Given there are 49 failed custom checks
          When navigating to the custom checks tab
          Then the pagination controls should not be visible
        */
  });
  describe("Rule: Custom checks should auto-refresh", () => {
    it.todo("Example: When a custom check fails, the custom checks tab should auto-refresh with the new failed custom check");

    /* SCENARIO
          Given 2 passing custom checks
          And the custom checks page is open
          When the endpoint reports a failing custom check
          Then the failing custom check should be rendered
        */

    it.todo("Example: A failing custom check that begins passing should be removed from the list on the custom checks tab");
    /* SCENARIO
          Given 2 failing custom checks
          And the custom checks page is open
          When one of the custom checks passes
          Then the passing custom check should be removed from the list
        */
  });
});
