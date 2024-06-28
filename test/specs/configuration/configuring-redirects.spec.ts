import { test, describe } from "../../drivers/vitest/driver";

describe("FEATURE: Configuring queue redirects", () => {
  describe("RULE: All queue redirects should be listed", () => {
    test.todo("EXAMPLE: A message should be shown when there are no redirects");

    /* SCENARIO
          Empty

          When there are no redirects
          Then "There are currently no redirects" should appear
        */

    test.todo("EXAMPLE: Exiting redirects should be shown in a list");
    /* SCENARIO
          Non-empty

          When there are redirects
          Then they are shown
        */

    test.todo("EXAMPLE: Redirects should be shown in a list when there are created");

    /* SCENARIO
          Empty

          Given there are no redirects
          When a redirect is created
          Then the new redirect is shown in the list
        */

    /* NOTES
          From Address
          To Address
          Last Modified
          End Redirect
          Modify Redirect
        */
  });
  describe("RULE: Queue redirects should be able to be created", () => {
    test.todo("EXAMPLE: The 'create' button in the create redirect dialog should be disabled when the form is invalid");

    /* SCENARIO
          Cannot save invalid

          When Create Redirect is clicked
          And invalid redirect info is entered
          Then the Save button is disabled
        */

    test.todo("EXAMPLE: Clicking the 'create' button with Valid redirect information in the create redirect dialog should create a redirect");

    /* SCENARIO
          Valid redirect

          When Create Redirect is clicked
          And valid redirect info is entered
          And Save is clicked
          Then the redirect is created
        */

    test.todo("EXAMPLE: A valid 'To' address that is not known should show a warning message but still allow the redirect to be created");
    /* SCENARIO
          Warn if to-address is not known

          When Create Redirect is clicked
          And valid redirect info is entered
          And the to address is not known to ServiceControl
          Then a warning message is shown
          And the redirect can still be created
        */

    test.todo("EXAMPLE: Clicking the 'create' button with the 'Immediately retry any matching failed messages' checkbox checked should create a redirect and start a retry operation");
    /* SCENARIO
          Immediate retry

          When Create Redirect is clicked
          And valid redirect info is entered
          And the "Immediately retry any matching failed messages" checkbox is checked
          And the Create button is clicked
          Then the redirect is created
          And a retry operation starts matching the from physical address
        */

    test.todo("EXAMPLE: Clicking the 'create' button with the 'Immediately retry any matching failed messages' checkbox unchecked should create a redirect and not start a retry operation");

    /* SCENARIO
          No immediate retry

          When Create Redirect is clicked
          And valid redirect info is entered
          And the "Immediately retry any matching failed messages" checkbox is unchecked
          And the Create button is clicked
          Then the redirect is created
          And no retry operation starts
        */

    test.todo("EXAMPLE: Creating a redirect with a 'From' address that already exists should show an error message and not create a new redirect");
    /* SCENARIO
          Cannot create multiple redirects for same from address

          Given a redirect exists with a From address of "Endpoint1"
          When a new redirect is created with a From address of "Endpoint1"
          Then no new redirect is created
          And the user is notified that this action is invalid
        */

    test.todo("EXAMPLE: Creating a redirect with a 'To' address that already exists should show an error message and not create a new redirect");
    /* SCENARIO
          Cannot chain redirects

          Given a rediect exists with a From address of "Endpoint1"
          When a new redirect is created with a To address of "Endpoint1"
          Then no new redirect is created
          And the user is notified that this action is invalid
        */

    test.todo("EXAMPLE: Creating a redirect with a 'From' address when a redirect with the same 'To' address already exists should show an error message and not create a new redirect");
    /* SCENARIO
          Cannot chain redirects 2

          Given a rediect exists with a To address of "Endpoint1"
          When a new redirect is created with a From address of "Endpoint1"
          Then no new redirect is created
          And the user is notified that this action is invalid
        */

    test.todo("EXAMPLE: Clicking the 'cancel' button in the create redirect dialog should close the dialog and not create a redirect");
    /* SCENARIO
          Cancel

          When Create Redirect is clicked
          And valid redirect info is entered
          And Cancel is clicked
          Then the Create redirect dialog is closed
          And no redirect is created
        */
  });
  describe("RULE: Existing queue redirects should not allow the 'From' address to modified", () => {
    test.todo("EXAMPLE: Opening the 'Modify redirect' dialog should not allow the 'From' address to be changed");
    /* SCENARIO
          Cannot change from address

          Given an existing redirect
          When Modify Redirect is clicked
          Then the Modify redirect dialog is shown
          And the From address cannot be changed
        */
  });
  describe("RULE: Existing queue redirects should be able to be modified", () => {
    test.todo("EXAMPLE: Changes to the 'To' address should be saved when the 'modify' button is clicked");

    /* SCENARIO
          Can change to address

          Given an existing redirect
          When Modify Redirect is clicked
          And the To address is changed
          And the Modify button is clicked
          Then the redirect is updated
        */

    test.todo("EXAMPLE: 'To' address that is not known should show a warning message but still allow the redirect to be modified");
    /* SCENARIO
          Warn if to-address is not known

          Given an existing redirect
          When Modify Redirect is clicked
          And the to address is not known to ServiceControl
          Then a warning message is shown
          And the redirect can still be modified
        */

    test.todo("EXAMPLE: Modifying a redirect with a 'to' address that already exists to another redirect's 'from' address should show an error message and not modify the redirect");
    /* SCENARIO
          Cannot chain redirects

          Given a rediect exists with a From address of "Endpoint1"
          When another redirect is modfied with a To address of "Endpoint1"
          Then the redirect is not modified
          And the user is notified that this action is invalid
        */

    test.todo("EXAMPLE: Modifying a redirect and checking the 'Immediately retry any matching failed messages' checkbox should update the redirect and start a retry operation");
    /* SCENARIO
          Immediate retry

          Given an existing redirect
          When Modify Redirect is clicked
          And the "Immediately retry any matching failed messages" checkbox is checked
          And the Modify button is clicked
          Then the redirect is updated
          And a retry operation starts matching the from physical address
        */

    test.todo("EXAMPLE: Modifying a redirect and unchecking the 'Immediately retry any matching failed messages' checkbox should update the redirect and not start a retry operation");
    /* SCENARIO
          No immediate retry

          Given an existing redirect
          When Modify Redirect is clicked
          And the "Immediately retry any matching failed messages" checkbox is unchecked
          And the Modify button is clicked
          Then the redirect is updated
          And no retry operation starts
        */

    test.todo("EXAMPLE: Clicking the 'cancel' button in the modify redirect dialog should close the dialog and not modify the redirect");

    /* SCENARIO
          Cancel

          Given an existing redirect
          When Modify Redirect is clicked
          And the details of the redirect are changed
          And Cancel is clicked
          Then the Modify redirect dialog is closed
          And the redirect is not modified
        */
  });
  describe("RULE: Redirects should be able to be ended", () => {
    test.todo("EXAMPLE: Clicking the 'Yes' button in the end redirect dialog should end the redirect");

    /* SCENARIO
          Confirmed

          Given an existing redirect
          When End Redirect is clicked
          And Yes is clicked
          And the redirect is ended
        */

    test.todo("EXAMPLE: Clicking the 'No' button in the end redirect dialog should not end the redirect");
    /* SCENARIO
          Not confirmed

          Given an existing redirect
          When End Redirect is clicked
          And No is clicked
          And the redirect is still present
        */
  });
  describe("RULE: The number of redirects should be displayed", () => {
    test.todo("EXAMPLE: The tab should include a (0) suffix when there are no redirects");

    /* SCENARIO
          Empty

          When there are no redirects
          Then the tab should include a (0) suffix
        */

    test.todo("EXAMPLE: The tab should increment the counter when a redirect is added");
    /* SCENARIO
          A redirect is added

          When a redirect is added
          Then the counter next to the tab should be incremented
        */

    test.todo("EXAMPLE: The tab should decrement the counter when a redirect is ended");
    /* SCENARIO
          A redirect is ended

          When a redirect is ended
          Then the counter next to the tab should be decremented
        */
  });
});
