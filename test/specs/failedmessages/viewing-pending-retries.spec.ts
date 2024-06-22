import { it, describe } from "../../drivers/vitest/driver";

describe("FEATURE: Pending Retries", () => {
  describe("Rule: Pending Retries tab being shown is conditional on a config value", () => {
    it.todo("Example: When the config value 'showPendingRetry' is set to false, the Pending Retries tab should not shown");

    /* SCENARIO
          When the config value "showPendingRetry" is set to false
          Then the Pending Retries tab is not shown
        */

    /* QUESTIONS
          should the /failed-messages/pending-retries route also be disabled?
        */

    it.todo("Example: When the config value 'showPendingRetry' is set to true, the Pending Retries tab should be shown");

    /* SCENARIO
          When the config value "showPendingRetry" is set to true
          Then the Pending Retries tab is shown
        */

    it.todo("Example: When the config value 'showPendingRetry' is not set, the Pending Retries tab should not shown");
    /* SCENARIO
          When the config value "showPendingRetry" is not set
          Then the Pending Retries tab is not shown
        */
  });
});
