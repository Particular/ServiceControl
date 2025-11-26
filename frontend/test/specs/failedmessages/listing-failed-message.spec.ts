import { test, describe } from "../../drivers/vitest/driver";

describe("FEATURE: All Failed Messages", () => {
  describe("RULE: All failed messages view should show an unfiltered list", () => {
    test.todo("EXAMPLE: All failed messages tab should be highlighted as active");
    test.todo("EXAMPLE: Browser tab title should show 'All Failed Messages'");
    test.todo("EXAMPLE: Failed messages should be ordered according to the selected sort by field");

    /* SCENARIO
          Given the entry route to the failed messages view is from the "All Failed Messages" tab
          Then the view should show all current failed messages
          and the "All Failed Messages" tab should be highlighted as active
          and the browser tab title should show "All Failed Messages"
        */

    test.todo("EXAMPLE: Failed messages should be ordered according to the selected sort by field");
    /* SCENARIO
          Given the failed messages are shown
          Then they are ordered according to the selected Sort By field
        */

    test.todo("EXAMPLE: A failed message should display the current message name in bold");
    test.todo("EXAMPLE: A failed message should display a time period indicating how long ago the failure happened");
    test.todo("EXAMPLE: A failed message should display the name of the Endpoint that the message failed on");
    test.todo("EXAMPLE: A failed message should display the name of the Machine that the message failed on");
    test.todo("EXAMPLE: A failed message should display the exception message text");
    /* SCENARIO
          Given there are 1 or more Failed Message rows shown
          Then the row will display the current message name in bold
          and the row will display a time period indicating how long ago the failure happened (retry failure if there is one)
          and the row will display the name of the Endpoint that the message failed on
          and the row will display the name of the Machine that the message failed on
          and the row will display the exception message text
        */

    test.todo("EXAMPLE: A failed message should display the number of times it has failed retries");
    /* SCENARIO
          Given there is a Failed Message row shown
          and that row has previously been retried
          Then the row will display the number of times it has failed retries (note: 1 less than total failures for the message)
          and this retry failure information will be visually more prominent than the other information
        */

    test.todo("EXAMPLE: A message should be shown when there are no failed messages");
    /* SCENARIO
          Given there are no Failed Messages
          Then the "All Failed Message" tab will display a message indicating the fact
        */
  });
  describe("RULE: Failed messages (group route) view should only show failed messages associated with that group", () => {
    test.todo("EXAMPLE: Only messages of a selected group should be shown");
    test.todo("EXAMPLE: Group name should be shown as a heading");
    test.todo("EXAMPLE: Group message count should be shown as a subtext to the group heading");
    test.todo("EXAMPLE: Failed Message Groups tab should remain highlighted as active");
    /* SCENARIO
          Given the entry route to the failed messages view is from selecting a group in the "Failed Message Groups" tab
          Then the view should show only failed messages associated with the selected group
          and the group name should be shown as a heading
          and the group message count should be shown as a subtext to the group heading
          and the "Failed Message Groups" tab should remain highlighted as active
        */
  });
  describe("RULE: Row hover functionality", () => {
    test.todo("EXAMPLE: Hovering the cursor over a failed message row should indicate that it is active, selectable, and show the 'Request Retry' action");

    /* SCENARIO
          Given there are 1 or more Failed Message rows shown
          and the user hovers over a Failed Message row
          Then the row indicates that it is active (hover state)
          and that it is selectable (cursor/underlining)
          and the "Request Retry" action is made available on the row
        */

    /* QUESTIONS
          why is "Request Retry" not always shown, similar to "Request Retry" on the Failed Message Groups screen?
        */
  });
  describe('RULE: The badge counter on the "All Failed Messages" tab header and the "Failed messages" main navigation items should reflect the total count of failed messages', () => {
    test.todo("Not implemented");
  });
  describe("RULE: action functionality", () => {
    test.todo("EXAMPLE: Clicking the 'Request Retry' action should initiate a retry for the selected message");

    /* SCENARIO
          Given there are 1 or more Failed Message rows are shown
          and the user clicks the "Request Retry" action for a row
          Then the row indicates that it is pending a retry
          and the row is removed from the "Failed Messages" list once the retry has been initiated
        */
  });
  describe("RULE: button functionality", () => {
    test.todo("EXAMPLE: When no Failed Message rows are selected, the 'Select All' button should be enabled");
    test.todo("EXAMPLE: When no Failed Message rows are selected, the 'Retry Selected' button should be disabled");
    test.todo("EXAMPLE: When no Failed Message rows are selected, the 'Delete Selected' button should be disabled");
    test.todo("EXAMPLE: When no Failed Message rows are selected, the 'Export Selected' button should be disabled");

    /* SCENARIO
          Given no Failed Message rows are selected
          Then the "Select All" button is enabled
          and the "Retry Selected" button is disabled
          and the "Delete Selected" button is disabled
          and the "Export Selected" button is disabled
        */

    test.todo("EXAMPLE: When 1 or more Failed Message rows are selected, the 'Select All' button should be replaced by a 'Clear Selection' button");
    test.todo("EXAMPLE: When 1 or more Failed Message rows are selected, the 'Retry Selected' button should indicate the number of rows selected and be enabled");
    test.todo("EXAMPLE: When 1 or more Failed Message rows are selected, the 'Delete Selected' button should indicate the number of rows selected and be enabled");
    /* SCENARIO
          Given 1 or more Failed Message rows are selected
          Then the "Select All" button is replaced by a "Clear Selection" button
          and the "Retry selected" button indicates the number of rows selected and is enabled
          and the "Delete selected" button indicates the number of rows selected and is enabled
          and the "Export selected" button indicates the number of rows selected and is enabled
        */
  });
});
