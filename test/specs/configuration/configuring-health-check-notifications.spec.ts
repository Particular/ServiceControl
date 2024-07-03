import { test, describe } from "../../drivers/vitest/driver";

describe("FEATURE: Health check notifications", () => {
  describe("RULE: Email notification should be able to toggled on and off", () => {
    test.todo("EXAMPLE: Email notification is toggled on");

    /* SCENARIO
          Enable email notifications
          
          Given Email notifications are OFF
          When the toggle button is clicked
          Then Email notifications are ON
        */

    test.todo("EXAMPLE: Email notification is toggled off");
    /* SCENARIO
          Disable email notifications
          
          Given Email notifications are ON
          When the toggle button is clicked
          Then Email notifications are OFF
        */
  });
  describe("RULE: Email notifications should be configurable", () => {
    test.todo("EXAMPLE: Clicking the configure button should open the email configuration popup");

    /* SCENARIO
          Open email configuration

          Given the Email configuration popup is not visible
          When the "Configure" button is clicked
          Then the Email configuration popup is displayed
        */

    test.todo("EXAMPLE: The save button should be enabled when the form is valid");
    test.todo("EXAMPLE: The save button should be disabled when the form is invalid");
    test.todo("EXAMPLE: The save button should update the email configuration and close the popup when clicked");

    /* SCENARIO
          Invalid configurations cannot be saved

          Given the Email configuration popup is visible
          When invalid or incomplete data is entered into the form
          Then the Save button is not enabled
    */

    test.todo("EXAMPLE: The cancel button should close the email configuration popup without saving changes");

    /* SCENARIO
          Email configuration changed can be cancelled

          Given the Email configuration popup is visible
          And edits have been made to the email configuration
          When the Cancel button is pressed
          Then the Email configuration popup is closed
          And no changes have been made to the email configuration
        */
  });
  describe("RULE: Health check notification configuration should be persistent", () => {
    test.todo("EXAMPLE: Updated email configuration should remain after a page refresh");

    /* SCENARIO
          Email configuration

          When the email configuration has been changed
          And the screen is refreshed
          Then the email notification configuration matches what was last saved
        */

    test.todo("EXAMPLE: Email notification are on and remain on after a page refresh");
    test.todo("EXAMPLE: Email notification are off and remain off after a page refresh");
    /* SCENARIO
          Email notifications toggle

          Given the Email notifications are ON
          When the page is refreshed
          Then the Email notifications are ON
        */
  });
  describe("RULE: Sending a test notification should indicate success or failure", () => {
    test.todo("EXAMPLE: Invalid Configuration");

    /* SCENARIO
          Given an invalid configuration
          When "Send test notification" is clicked
          Then "TEST FAILED" is displayed
        */

    test.todo("EXAMPLE: Valid Configuration");
    /* SCENARIO          

          Given a valid configuration
          When "Send test notification" is clicked
          Then "Test email sent successfully" is displayed
        */
  });
});
