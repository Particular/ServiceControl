import { it, describe } from "../../drivers/vitest/driver";

describe("FEATURE: Heartbeats configuration", () => {
  describe("RULE: A list of all endpoints with the heartbeats plug-in installed should be displayed", () => {
    it.todo("EXAMPLE: With no endpoints, the text 'Nothing to configure' should be displayed");

    /* SCENARIO
      No endpoints

      Given no endpoint instances
      When the configuration screen is loaded
      Then the text "Nothing to configure" should be displayed
    */

    it.todo("EXAMPLE: 3 endpoints should be displayed in the list");
    /* SCENARIO
      Some endpoints

      Given 3 endpoint instances
        Name |
        Foo1
        Foo2
        Foo3
      When the configuration screen is loaded
      Then All 3 endpoints should be displayed
    */

    /* NOTES
      Endpoint name
      Host id
      last reported heartbeat
      Monitoring status
    */
  });
  describe("RULE: Toggling on/off heartbeat monitoring for endpoints should be possible", () => {
    it.todo("EXAMPLE: Heartbeat monitoring toggle should be off by default");

    /* SCENARIO
      Given a monitored endpoint instance
      When the monitoring toggle is clicked
      Then the endpoint instance is no longer monitored
      And should not appear in the inactive endpoints list
      And should not appear in the active endpoints list
    */

    it.todo("EXAMPLE: Clicking the monitoring toggle for an endpoint should activate heartbeat monitoring of the endpoint");
    /* SCENARIO
      Given an unmonitored endpoint instance
      And the instance is sending heartbeats
      When the monitoring toggle is clicked
      Then the endpoint instance is monitored
      And should appear in the Active Endpoints list
    */

    /* SCENARIO
      Given an unmonitored endpoint instance
      And the instance is not sending heartbeats
      When the monitoring toggle is clicked
      Then the endpoint instance is monitored
      And should appear in the Inactive Endpoints list
    */
  });
  describe("RULE: Sorting by of the name of an endpoint should be possible in all displays", () => {
    it.todo("EXAMPLE: List of endpoints should be sorted by name in ascending order");

    /* SCENARIO
      Given 3 endpoint instance
        Name |
        Foo1
        Foo2
        Foo3
      When the sort by is set to Name
      Then the instances should be listed in order
    */

    /* NOTES
      Name (asc/desc)
      Latest heartbeat (asc/dec)
    */

    it.todo("EXAMPLE: List of endpoints should be sorted by name in descending order");
    /* SCENARIO
      Given 3 endpoint instance
        Name |
        Foo1
        Foo2
        Foo3
      When the sort by is set to Name (descending)
      Then the instances should be listed in reverse order
    */

    it.todo("EXAMPLE: List of endpoints should be sorted by latest heartbeat in ascending order");
    it.todo("EXAMPLE: List of endpoints should be sorted by latest heartbeat in descending order");
    /* SCENARIO
      Same again for Latest heartbeat
    */

    it.todo("EXAMPLE: Sort by should be persisted on page refresh and across tabs");
    /* SCENARIO
      Given the Sort By field has been changed
      When the page is refreshed
      Then the Sort By field retains its value
      And the Sort By field has the same value on all other Endpoint Heartbeats tabs
    */
  });
  describe("RULE: Filtering endpoints by name should be possible", () => {
    it.todo("EXAMPLE: Filter string matches a subset of endpoint names should display only those endpoints");

    /* SCENARIO
      Given 3 endpoint instances
        Name |
        Foo1
        Bar
        Foo2
      When the text "foo" is entered into the filter box
      Then Foo1 should be shown
       And Foo2 should be shown
      And Bar should not be shown
    */

    it.todo("EXAMPLE: Filter string matches no endpoint names should display no endpoints");
    /* SCENARIO
      Given 3 endpoint instances
        Name |
        Foo1
        Foo2
        Foo3
      When the text "bar" is entered into the filter box
      Then Foo1 should not be shown
       And Foo2 should not be shown
      And Foo3 should not be shown
    */

    it.todo("EXAMPLE: The filter string should be persisted on page refresh and across tabs");
    /* SCENARIO
      Given the filter string is ""
      When the text "Foo" is entered into the filter control
      Then the list is filtered (see above)
      And the filter control on the Active Endpoints tab contains "Foo"
      And the filter control on the Inactive endpoints tab contains "Foo"
    */
  });
  describe("RULE: A performance monitoring warning should be displayed", () => {
    it.todo("EXAMPLE: A warning should be displayed when the configuration screen is loaded");

    /* SCENARIO
      When the configuration screen is loaded
      Then a warning should be displayed about this being disconnected to performance monitoring
    */
  });
});
