import { test, describe } from "../../drivers/vitest/driver";

describe("FEATURE: Active Endpoints", () => {
  describe("RULE: The number of active endpoints should be shown", () => {
    test.todo("EXAMPLE: With 7 active endpoints, the tab should show (7)");

    /* SCENARIO
        Given 7 endpoint instances sending heartbeats
        When the hearbeats screen is open
        Then the Active Endpoints tab includes a (7) suffix
      */

    test.todo("EXAMPLE: With 7 active endpoints and 1 endpoint stop sending heartbeat, the tab should show (6)");
    /* SCENARIO
        Given 7 endpoint instances sending heartbeats
        When one of the endpoints stop sending heartbeats
        Then the Active Endpoints tab suffix changes to (6)
      */

    test.todo("EXAMPLE: With 6 active endpoints and 1 endpoint starts sending heartbeat, the tab should show (7)");
    /* SCENARIO
        Given 6 endpoint instances sending heartbeats
        And 1 endpoint instance not sending heartbeats
        When the stopped endpoint starts sending heartbeats
        Then the Active Endpoints tab suffix changes to (7)
      */
  });
  describe("RULE: A list of active endpoints should be shown", () => {
    test.todo("EXAMPLE: With 3 active endpoints sending heartbeats, 3 endpoints should be shown in the list of active endpoints");

    /* SCENARIO
        Display list of active endpoints

        Given 3 endpoint instances sending heartbeats
        When the Active Endpoints tab is open
        Then the 3 endpoints are displayed
      */
  });
  describe("RULE: Active endpoint list row should show endpoint instances with name, host identifier, and latest heartbeat received", () => {
    test.todo("EXAMPLE: With 3 active endpoint instances named 'Endpoint1' at host 'HOST1' sending a heartbeat, there should be 3 rows displaying 'Endpoint1@HOST1', and the latest heartbeat received");

    /* SCENARIO
        Display Endpoint Instances

        Given 3 endpoint instances sending heartbeats
        When the Active Endpoints tab is open
        Then the 3 endpoint instances are displayed
      */

    /* NOTES
        Endpoint name
        Host identifier
        Latest heartbeat received

      */
  });
  describe("RULE: Active endpoint list row should show logical endpoint with name, number of instances, host identifier, and latest heartbeat received", () => {
    test.todo("EXAMPLE: With multiple instances of an endpoints sending heartbeats, only the single logical endpoint details should be displayed in the list");
  });
  /* SCENARIO
        Display Logical Endpoints

        Given 3 endpoint instances sending heartbeats
          Endpoint1@HOST1
          Endpoint1@HOST2
          Endpoint2@HOST1
        When the Active Endpoints tab is open
        Then 2 logical endpoints are shown
          Endpoint1
          Endpoint2
      */

  /* NOTES
        Endpoint name
        Instance count
        Latest heartbeat received
      */
  describe("RULE: Changing between logical and instance listing displays should be possible", () => {
    test.todo("Not implemented");
  });
  describe("RULE: Sorting by of the name of an endpoint should be possible in all displays", () => {
    test.todo("Not implemented");
  });
  describe("RULE: Filtering endpoints by name should be possible", () => {
    test.todo("Not implemented");
  });
});
describe("FEATURE: Inactive endpoints", () => {
  describe("RULE: The count of inactive endpoints should be displayed", () => {
    test.todo("Not implemented");
  });
  describe("RULE: Listing inactive endpoints should be possible", () => {
    test.todo("Not implemented");
  });
  describe("RULE: Changing between logical and instance listing displays should be possible", () => {
    test.todo("Not implemented");
  });
  describe("RULE: Sorting by of the name of an endpoint should be possible in all displays", () => {
    test.todo("Not implemented");
  });
  describe("RULE: Filtering endpoints by name should be possible", () => {
    test.todo("Not implemented");
  });
});
