import { render, describe, test, screen, expect, within } from "@component-test-utils";
import sut from "@/components/messages/SagaDiagram.vue";
import { SagaHistory } from "@/resources/SagaHistory";
import makeRouter from "@/router";
import { createTestingPinia } from "@pinia/testing";
import { MessageStore } from "@/stores/MessageStore";
import { MessageStatus } from "@/resources/Message";

//Defines a domain-specific language (DSL) for interacting with the system under test (sut)
interface componentDSL {
  action1(value: string): void;
  assert: componentDSLAssertions;
}

//Defines a domain-specific language (DSL) for checking assertions against the system under test (sut)
interface componentDSLAssertions {
  thereAreTheFollowingSagaChangesInThisOrder(expectedDatesInOrder: Date[]): void;
  displayedSagaGuidIs(sagaId: string): void;
  displayedSagaNameIs(humanizedSagaName: string): void;
  linkIsShown(arg0: { withText: string; withHref: string }): void;
  NoSagaDataAvailableMessageIsShownWithMessage(message: RegExp): void;
  SagaPlugInNeededIsShownWithTheMessages({ messages, withPluginDownloadUrl }: { messages: RegExp[]; withPluginDownloadUrl: string }): void;
  SagaSequenceIsNotShown(): void;
}

describe("Feature: Message not involved in Saga", () => {
  describe("Rule: When the selected message has not participated in a Saga, display a legend indicating it.​", () => {
    test("EXAMPLE: A message that has not participated in a saga is selected", () => {
      const messageStore = {} as MessageStore;
      messageStore.state = {} as MessageStore["state"];
      messageStore.state.data = {} as MessageStore["state"]["data"];
      messageStore.state.data.invoked_saga = {
        has_saga: false,
        saga_id: undefined,
        saga_type: undefined,
      };

      // No need to manually set up the store - it will be empty by default
      const componentDriver = rendercomponent({
        initialState: {
          MessageStore: messageStore,
          SagaDiagramStore: undefined, // Lets pass undefined to simulate no saga data available
        },
      });

      componentDriver.assert.NoSagaDataAvailableMessageIsShownWithMessage(/This message is not part of any saga/i);
    });
  });
});

describe("Feature: Detecting no Audited Saga Data Available", () => {
  describe("Rule: When a message participates in a Saga, but the Saga data is unavailable, display a legend indicating that the Saga audit plugin is needed to visualize the saga.", () => {
    test("EXAMPLE: A message that was participated in a Saga without the Saga audit plugin being active gets selected", () => {
      const messageStore = {} as MessageStore;
      messageStore.state = {} as MessageStore["state"];
      messageStore.state.data = {} as MessageStore["state"]["data"];
      messageStore.state.data.invoked_saga = {
        has_saga: true,
        saga_id: "saga-id-123",
        saga_type: "Shipping.ShipOrderWorkflow",
      };

      // No need to manually set up the store - it will be empty by default
      const componentDriver = rendercomponent({
        initialState: {
          MessageStore: messageStore,
          SagaDiagramStore: undefined, // Lets pass undefined to simulate no saga data available
        },
      });

      componentDriver.assert.SagaPlugInNeededIsShownWithTheMessages({
        messages: [/Saga audit plugin needed to visualize saga/i, /To visualize your saga, please install the appropriate nuget package in your endpoint/i, /install-package NServiceBus\.SagaAudit/i],
        withPluginDownloadUrl: "https://www.nuget.org/packages/NServiceBus.SagaAudit",
      });
    });
  });
});

describe("Feature: Navigation and Contextual Information", () => {
  describe("Rule: Clearly indicate contextual information like Saga ID and Saga Type.", () => {
    test("EXAMPLE: A message with a Saga Id '123' and a Saga Type 'ServiceControl.SmokeTest.AuditingSaga' gets selected", () => {
      const messageStore = {} as MessageStore;
      messageStore.state = {} as MessageStore["state"];
      messageStore.state.data = {} as MessageStore["state"]["data"];
      messageStore.state.data.invoked_saga = {
        has_saga: true,
        saga_id: "123",
        saga_type: "ServiceControl.SmokeTest.AuditingSaga",
      };

      // Set initial state with sample saga history
      const componentDriver = rendercomponent({
        initialState: {
          MessageStore: messageStore,
          SagaDiagramStore: { sagaHistory: sampleSagaHistory },
        },
      });

      componentDriver.assert.displayedSagaNameIs("AuditingSaga");
      componentDriver.assert.displayedSagaGuidIs("123");
    });
  });
});

describe("Feature: 3 Visual Representation of Saga Timeline", () => {
  describe("Rule: 3.1 Clearly indicate the initiation and completion of a saga.", () => {
    test.todo("EXAMPLE: A message with a Saga Id '123' and a Saga Type 'ServiceControl.SmokeTest.AuditingSaga' gets selected", () => {
      //"Saga Initiated" is explicitly displayed first, and "Saga Completed" is explicitly displayed at the bottom.
    });
  });

  describe("Rule: 3.3 Display a chronological timeline of saga events localized to user environment.", () => {
    test.each([
      {
        timezone: "UTC",
      },
      {
        timezone: "America/Los_Angeles",
      },
    ])("EXAMPLE: Rendering a Saga with 4 changes - User Timezone $timezone", ({ timezone }) => {
      //     Each saga event ("Saga Initiated," "Saga Updated," "Timeout Invoked," "Saga Completed") is timestamped to represent progression over time. Events are ordered by the time they ocurred.
      //TODO:  "Incoming messages are displayed on the left, and outgoing messages are displayed on the right."  in another test?

      //arragement
      //sampleSagaHistory already not sorted TODO: Make this more clear so the reader of this test doesn't have to go arround and figure out the preconditions
      const messageStore = {} as MessageStore;
      messageStore.state = {} as MessageStore["state"];
      messageStore.state.data = {} as MessageStore["state"]["data"];
      messageStore.state.data.invoked_saga = {
        has_saga: true,
        saga_id: "123",
        saga_type: "ServiceControl.SmokeTest.AuditingSaga",
      };

      // Set the environment to a fixed timezone
      // JSDOM, used by Vitest, defaults to UTC timezone
      // To ensure consistency, explicitly set the timezone
      // This ensures that the rendered local time of the saga changes
      // will always be interpreted and displayed in the specified timezone, avoiding flakiness
      process.env.TZ = timezone;

      //access each of the saga changes and update its start time and finish time to the same values being read from the variable declaration,
      // but set them again explicitly here
      //so that the reader of this test can see the preconditions at play
      //and understand the test better without having to jump around

      const startTimeA = new Date("2025-03-28T03:04:08.000Z");
      const finishTimeA1 = new Date("2025-03-28T03:04:08.000Z");
      const startTimeB = new Date("2025-03-28T03:04:07.000Z");
      const finishTimeB1 = new Date("2025-03-28T03:04:07.000Z");
      const startTimeC = new Date("2025-03-28T03:04:06.000Z");
      const finishTimeC1 = new Date("2025-03-28T03:04:06.000Z");
      const startTimeD = new Date("2025-03-28T03:04:05.000Z");
      const finishTimeD1 = new Date("2025-03-28T03:04:05.000Z");

      sampleSagaHistory.changes[0].start_time = startTimeA;
      sampleSagaHistory.changes[0].finish_time = finishTimeA1;
      sampleSagaHistory.changes[1].start_time = startTimeB;
      sampleSagaHistory.changes[1].finish_time = finishTimeB1;
      sampleSagaHistory.changes[2].start_time = startTimeC;
      sampleSagaHistory.changes[2].finish_time = finishTimeC1;
      sampleSagaHistory.changes[3].start_time = startTimeD;
      sampleSagaHistory.changes[3].finish_time = finishTimeD1;
      sampleSagaHistory.changes[3].status = "new";

      //B(1), C(2),  A(0), D(3)
      //B(1), C1(2), C(2), A1(0)

      // Set up the store with sample saga history
      const componentDriver = rendercomponent({
        initialState: {
          MessageStore: messageStore,
          SagaDiagramStore: { sagaHistory: sampleSagaHistory },
        },
      });

      //assert
      componentDriver.assert.thereAreTheFollowingSagaChangesInThisOrder([startTimeD, startTimeC, startTimeB, startTimeA]);
    });
  });
});

function rendercomponent({ initialState = {} }: { initialState?: { MessageStore?: MessageStore; SagaDiagramStore?: { sagaHistory: SagaHistory } } }): componentDSL {
  const router = makeRouter();

  // Render with createTestingPinia
  render(sut, {
    global: {
      plugins: [
        router,
        createTestingPinia({
          initialState,
          stubActions: true, // Explicitly stub actions (this is the default)
        }),
      ],
      stubs: {
        CodeEditor: true,
        CopyToClipboard: true,
      },
      directives: {
        // Add stub for tippy directive
        tippy: () => {},
      },
    },
  });

  const dslAPI: componentDSL = {
    action1: () => {
      // Add actions here;
    },
    assert: {
      NoSagaDataAvailableMessageIsShownWithMessage(message: RegExp) {
        //ensure that the only one status message is shown
        expect(screen.queryAllByRole("status")).toHaveLength(1);

        const status = screen.queryByRole("status", { name: /message-not-involved-in-saga/i });
        expect(status).toBeInTheDocument();
        const statusText = within(status!).getByText(message);
        expect(statusText).toBeInTheDocument();

        this.SagaSequenceIsNotShown();
      },
      SagaPlugInNeededIsShownWithTheMessages({ messages, withPluginDownloadUrl }: { messages: RegExp[]; withPluginDownloadUrl: string }) {
        // Use the matcher to find the container element
        const messageContainer = screen.queryByRole("status", { name: /saga-plugin-needed/i });
        expect(messageContainer).toBeInTheDocument();

        // using within to find the text inside the container per each item in messages
        messages.forEach((message) => {
          const statusText = within(messageContainer!).getByText(message);
          expect(statusText).toBeInTheDocument();
        });

        // Verify the link
        const link = screen.getByRole("link", { name: "install-package NServiceBus.SagaAudit" });
        expect(link).toBeInTheDocument();
        expect(link).toHaveAttribute("href", withPluginDownloadUrl);

        this.SagaSequenceIsNotShown();
      },
      SagaSequenceIsNotShown() {
        const sagaSequence = screen.queryByRole("list", { name: /saga-sequence-list/i });
        expect(sagaSequence).not.toBeInTheDocument();
      },
      linkIsShown(args: { withText: string; withHref: string }) {
        const link = screen.getByRole("link", { name: args.withText });
        expect(link).toBeInTheDocument();
        expect(link.getAttribute("href")).toBe(args.withHref);
      },
      displayedSagaNameIs(name: string) {
        const sagaName = screen.getByRole("heading", { name: /saga name/i });
        expect(sagaName).toBeInTheDocument();
        expect(sagaName).toHaveTextContent(name);
      },
      displayedSagaGuidIs(guid: string) {
        const sagaGuid = screen.getByRole("note", { name: /saga guid/i });
        expect(sagaGuid).toBeInTheDocument();
        expect(sagaGuid).toHaveTextContent(guid);
      },
      thereAreTheFollowingSagaChangesInThisOrder: function (expectedDatesInOrder: Date[]): void {
        //Retrive the main parent component that contains the saga changes
        const sagaChangesContainer = screen.getByRole("table", { name: /saga-sequence-list/i });

        const sagaUpdatesElements = within(sagaChangesContainer).queryAllByRole("row");
        //from within each sagaUpdatesElements get the values of an element with aria-label="time stamp"
        //and check if the values are in the same order as the expectedDatesInOrder array passed to this function
        const sagaUpdatesTimestamps = sagaUpdatesElements.map((item: HTMLElement) => within(item).getByLabelText("time stamp"));

        //expect the number of found sagaUpdatesTimestamps to be the same as the number of expected dates passed to this function
        expect(sagaUpdatesTimestamps).toHaveLength(expectedDatesInOrder.length);

        const sagaUpdatesTimestampsValues = sagaUpdatesTimestamps.map((item) => item.innerHTML);

        // Verify we have the same number of rendered timestamps as expected dates
        expect(sagaUpdatesTimestampsValues).toHaveLength(expectedDatesInOrder.length);

        // For each rendered timestamp, verify it matches the expected date at that position
        // by formatting the expected date the same way and comparing strings
        expectedDatesInOrder.forEach((expectedDate, index) => {
          const expectedFormattedString = expectedDate.toLocaleString();
          expect(sagaUpdatesTimestampsValues[index]).toBe(expectedFormattedString);
        });
      },
    },
  };

  return dslAPI;
}

const sampleSagaHistory: SagaHistory = {
  id: "45f425fc-26ce-163b-4f64-857b889348f3",
  saga_id: "45f425fc-26ce-163b-4f64-857b889348f3",
  saga_type: "ServiceControl.SmokeTest.AuditingSaga",
  changes: [
    {
      start_time: new Date("2025-03-28T03:04:08.3819211Z"),
      finish_time: new Date("2025-03-28T03:04:08.3836Z"),
      status: "completed",
      state_after_change: '{"Id":"45f425fc-26ce-163b-4f64-857b889348f3","Originator":null,"OriginalMessageId":"4b9fdea7-d78c-41f0-91ee-b2ae00328f9c"}',
      initiating_message: {
        message_id: "876d89bd-7a1f-43f1-b384-b2ae003290e8",
        is_saga_timeout_message: true,
        originating_endpoint: "Endpoint1",
        originating_machine: "mobvm2",
        time_sent: new Date("2025-03-28T03:04:06.321561Z"),
        message_type: "ServiceControl.SmokeTest.MyCustomTimeout",
        intent: "Send",
        body_url: "body_url",
        message_status: MessageStatus.Successful,
      },
      outgoing_messages: [],
      endpoint: "Endpoint1",
    },
    {
      start_time: new Date("2025-03-28T03:04:07.5416262Z"),
      finish_time: new Date("2025-03-28T03:04:07.5509712Z"),
      status: "updated",
      state_after_change: '{"Id":"45f425fc-26ce-163b-4f64-857b889348f3","Originator":null,"OriginalMessageId":"4b9fdea7-d78c-41f0-91ee-b2ae00328f9c"}',
      initiating_message: {
        message_id: "1308367f-c6a2-418f-9df2-b2ae00328fc9",
        is_saga_timeout_message: true,
        originating_endpoint: "Endpoint1",
        originating_machine: "mobvm2",
        time_sent: new Date("2025-03-28T03:04:05.37723Z"),
        message_type: "ServiceControl.SmokeTest.MyCustomTimeout",
        intent: "Send",
        body_url: "body_url",
        message_status: MessageStatus.Successful,
      },
      outgoing_messages: [],
      endpoint: "Endpoint1",
    },
    {
      start_time: new Date("2025-03-28T03:04:06.3088353Z"),
      finish_time: new Date("2025-03-28T03:04:06.3218175Z"),
      status: "updated",
      state_after_change: '{"Id":"45f425fc-26ce-163b-4f64-857b889348f3","Originator":null,"OriginalMessageId":"4b9fdea7-d78c-41f0-91ee-b2ae00328f9c"}',
      initiating_message: {
        message_id: "e5bb5304-7892-4d39-96e2-b2ae003290df",
        is_saga_timeout_message: false,
        originating_endpoint: "Sender",
        originating_machine: "mobvm2",
        time_sent: new Date("2025-03-28T03:04:06.293765Z"),
        message_type: "ServiceControl.SmokeTest.SagaMessage2",
        intent: "Send",
        body_url: "body_url",
        message_status: MessageStatus.Successful,
      },
      outgoing_messages: [
        {
          delivery_delay: "00:00:02",
          destination: "Endpoint1",
          message_id: "876d89bd-7a1f-43f1-b384-b2ae003290e8",
          time_sent: new Date("2025-03-28T03:04:06.3214397Z"),
          message_type: "ServiceControl.SmokeTest.MyCustomTimeout",
          intent: "Send",
          deliver_at: new Date("2025-03-28T03:04:06.293765Z"),
          is_saga_timeout_message: false,
          originating_endpoint: "Sender",
          originating_machine: "mobvm2",
          body_url: "body_url",
          message_status: MessageStatus.Successful,
        },
      ],
      endpoint: "Endpoint1",
    },
    {
      start_time: new Date("2025-03-28T03:04:05.3332078Z"),
      finish_time: new Date("2025-03-28T03:04:05.3799483Z"),
      status: "new",
      state_after_change: '{"Id":"45f425fc-26ce-163b-4f64-857b889348f3","Originator":null,"OriginalMessageId":"4b9fdea7-d78c-41f0-91ee-b2ae00328f9c"}',
      initiating_message: {
        message_id: "4b9fdea7-d78c-41f0-91ee-b2ae00328f9c",
        is_saga_timeout_message: false,
        originating_endpoint: "Sender",
        originating_machine: "mobvm2",
        time_sent: new Date("2025-03-28T03:04:05.235534Z"),
        message_type: "ServiceControl.SmokeTest.SagaMessage1",
        intent: "Send",
        body_url: "body_url",
        message_status: MessageStatus.Successful,
      },
      outgoing_messages: [
        {
          delivery_delay: "00:00:02",
          destination: "Endpoint1",
          message_id: "1308367f-c6a2-418f-9df2-b2ae00328fc9",
          time_sent: new Date("2025-03-28T03:04:05.3715034Z"),
          message_type: "ServiceControl.SmokeTest.MyCustomTimeout",
          intent: "Send",
          deliver_at: new Date("2025-03-28T03:04:06.293765Z"),
          is_saga_timeout_message: false,
          originating_endpoint: "Sender",
          originating_machine: "mobvm2",
          body_url: "body_url",
          message_status: MessageStatus.Successful,
        },
      ],
      endpoint: "Endpoint1",
    },
  ],
};
