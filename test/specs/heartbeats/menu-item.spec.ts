import { it, describe } from "../../drivers/vitest/driver";

describe("FEATURE: Menu item", () => {
  describe("RULE: The count of inactive endpoints should be displayed in the navigation menu", () => {
    it.todo("EXAMPLE: An instance stops sending heartbeats, the menu item should show a badge with (1) of inactive endpoints");

    /* SCENARIO
      Given 5 monitored endpoint instances sending heartbeats
      When 1 of the endpoint instances stops sending heartbeats
      Then the menu item in the page header updates to include a badge indicating how many have stopped
    */

    it.todo("EXAMPLE: An instance starts sending heartbeats, the menu item should remove the badge");
    /* SCENARIO
      Given a set of monitored endpoint instances
      When all instances are sending heartbeats
      Then the menu item in the page header does not include a badge
    */

    it.todo("EXAMPLE: An unmonitored instance stops sending heartbeats, the menu item should not show a badge with a count");

    /* SCENARIO
      Given a set of monitored endpoint instances
      And 1 unmonitored endpoint instance
      When the unmonitored endpoint instance is not sending heartbeats
      Then the menu item badge is not displayed
    */
  });
});
