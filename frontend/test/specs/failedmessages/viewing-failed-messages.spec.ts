import { test, describe } from "../../drivers/vitest/driver";

describe("FEATURE: Viewing the details of a message group", () => {
  describe("RULE: Viewing the details of a Failed message group should be possible", () => {
    test.todo("EXAMPLE: Selecting a group from the failed messages view should show all messages associated with that group");
    test.todo("EXAMPLE: The group heading should be the group name of the group that was selected");
    test.todo("EXAMPLE: The browser tab title should show 'Failed Messages', not 'All Failed Messages'");

    /* SCENARIO
          Selecting a group from the failed messages view
          
          Given there are 1 or more groups are shown in the "Failed Message Groups" tab
          When a group is selected
          Then all messages associated with that group should be shown in the Failed Messages view
          and the group heading should be the group name of the group that was selected
          and the browser tab title should show "Failed Messages", not "All Failed Messages"
        */
  });
});
