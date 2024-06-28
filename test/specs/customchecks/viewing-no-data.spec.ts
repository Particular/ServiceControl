import { test, describe } from "../../drivers/vitest/driver";

describe("FEATURE: No data", () => {
  describe("RULE: When there is no data to show a message should be displayed ", () => {
    test.todo("EXAMPLE: 'No failed custom checks' should be displayed when there are no custom checks");

    /* SCENARIO
          Given there are no custom checks
          When navigating to the custom checks tab
          Then a message is shown "No failed custom checks"
        */

    test.todo("EXAMPLE: 'No failed custom checks' should be displayed when all custom checks are in a success state");
    /* SCENARIO
          Given there are custom checks
          And all custom checks are in a success state
          When navigating to the custom checks tab
          The a message is shown "No failed custom checks"
        */
  });
});
