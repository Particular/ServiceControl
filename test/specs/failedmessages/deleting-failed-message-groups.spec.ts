import { test, describe } from "../../drivers/vitest/driver";

describe("FEATURE: Deleted Message Groups", () => {
  describe("RULE: Deleted Message Groups view should shows all current deleted messages, grouped by the selected grouping", () => {
    test.todo("EXAMPLE: A message should be show when there are no deleted messages");

    /* SCENARIO
          Given there are no Deleted Messages
          Then the "Deleted Message Groups" tab will display a message indicating the fact
        */

    test.todo("EXAMPLE: The number of deleted messages in a group should be shown");
    test.todo("EXAMPLE: The time period from the first failed message should be shown");
    test.todo("EXAMPLE: The time period of the last failed message should be shown");
    test.todo("EXAMPLE: The time period of when the group was last retried should be shown");
    test.todo("EXAMPLE: A deleted message group that has not been retried should show N/A for the last retry time");

    /* SCENARIO
          Given there are 1 or more groups shown on the "Deleted Message Groups" tab
          Then the group row will display the current grouping name in bold
          and the group will display the number of messages in the group
          and the group will display a time period indicating how long ago the first failure happened
          and the group will display a time period indicating how long ago the last failure happened
          and the group will display a time period indicating how long ago the group was last retried, or N/A if never retried
        */
  });
  describe("RULE: All messages in a Deleted Message Group should be able to be restored in a single action", () => {
    test.todo("EXAMPLE: A restore button should be shown when there are deleted messages in a group");
    /* SCENARIO
          Given there are 1 or more groups shown on the "Deleted Message Groups" tab
          Then "Restore group" is shown as an available action on the group
        */

    /* SCENARIO
          Given there are 1 or more groups shown on the "Deleted Message Groups" tab
          When the user clicks the "Restore group" action
          and clicks "Yes" on the action confirmation modal
          Then all the messages in the selected group are returned to the Failed Messages list
          and the list refreshes with progress/confirmation of the successful restoration
        */
  });
  describe("RULE: Ability to select a given group should be hinted ", () => {
    test.todo("EXAMPLE: A group should indicate that it is active and selectable when the cursor is hovered over");

    /* SCENARIO
          Mouse hovering a group

          Given there are 1 or more groups show in the "Deleted Message Groups" tab
          and the user hovers over a Deleted Message Group row
          Then the row indicates that it is active (hover state)
          and that it is selectable (cursor/underlining)
        */
  });
});
