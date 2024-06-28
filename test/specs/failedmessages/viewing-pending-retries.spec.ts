import { test, describe } from "../../drivers/vitest/driver";

describe("FEATURE: Pending Retries", () => {
  describe("RULE: Pending Retries tab being shown is conditional on a config value", () => {
    test.todo("EXAMPLE: When the config value 'showPendingRetry' is set to false, the Pending Retries tab should not shown");

    /* SCENARIO
          When the config value "showPendingRetry" is set to false
          Then the Pending Retries tab is not shown
        */

    /* QUESTIONS
          should the /failed-messages/pending-retries route also be disabled?
        */

    test.todo("EXAMPLE: When the config value 'showPendingRetry' is set to true, the Pending Retries tab should be shown");

    /* SCENARIO
          When the config value "showPendingRetry" is set to true
          Then the Pending Retries tab is shown
        */

    test.todo("EXAMPLE: When the config value 'showPendingRetry' is not set, the Pending Retries tab should not shown");
    /* SCENARIO
          When the config value "showPendingRetry" is not set
          Then the Pending Retries tab is not shown
        */
  });
});
