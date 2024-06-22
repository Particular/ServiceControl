import { it, describe } from "../../drivers/vitest/driver";

describe("FEATURE: License", () => {
  describe("Rule: Platform license type should be shown shown", () => {
    it.todo("Example: Valid platform license type should be shown");

    /* SCENARIO
          Given the platform license is valid
          Then the platform license type is shown
        */
  });
  describe("Rule: License expiry date should be shown", () => {
    it.todo("Example: Valid license expiry date should be shown");

    /* SCENARIO
          Given a valid platform license
          Then the license expiry date is shown
        */
  });
  describe("Rule: Remaining license period should be displayed", () => {
    it.todo("Example: An expired license should show 'expired'");

    /* SCENARIO
          Expired license

          Given an expired platform license
          Then "expired" is shown
        */

    it.todo("Example: License expiring with 10 days should show 'expiring in X days'");
    /* SCENARIO
          License expiring soon

          Given a platform license with an expiry date within 10 days
          Then "expiring in X days" is shown
        */

    it.todo("Example: License expiring tomorrow should show 'expiring tomorrow'");
    /* SCENARIO
          License expiring tomorrow

          Given a platform license which expires tomorrow
          Then "expiring tomorrow" is shown
        */

    it.todo("Example: License expiring today should show 'expiring today'");
    /* SCENARIO
          License expiring today

          Given a platform license which expires today
          Then "expiring today" is shown
        */

    it.todo("Example: License expiring in more than 10 days should show 'X days left");
    /* SCENARIO
          License expiring in the future

          Given a platform license which expires more than 10 days from now
          Then "X days left" is shown
        */
  });
  describe("Rule: Non-license options should be hidden if license has expired", () => {
    it.todo("Example: Only 'LICENSE' tab is visible when license has expired");

    /* SCENARIO
          Given an expired license
          Then "LICENSE" is the only visible tab in the Configuration screen
        */
  });
});
