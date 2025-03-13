import { test, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { openEditAndRetryEditor } from "./actions/openEditAndRetryEditor";
import { getEditAndRetryEditor } from "./questions/getEditAndRetryEditor";
import { expect } from "vitest";

describe("FEATURE: Editing failed messages", () => {
  function getBoundingClientRect(): DOMRect {
    const rec = {
      x: 0,
      y: 0,
      bottom: 0,
      height: 0,
      left: 0,
      right: 0,
      top: 0,
      width: 0,
    };
    return { ...rec, toJSON: () => rec };
  }

  class FakeDOMRectList extends Array<DOMRect> implements DOMRectList {
    item(index: number): DOMRect | null {
      return this[index];
    }
  }

  document.elementFromPoint = (): null => null;
  HTMLElement.prototype.getBoundingClientRect = getBoundingClientRect;
  HTMLElement.prototype.getClientRects = (): DOMRectList => new FakeDOMRectList();
  Range.prototype.getBoundingClientRect = getBoundingClientRect;
  Range.prototype.getClientRects = (): DOMRectList => new FakeDOMRectList();

  describe("RULE: Editing of a message should only be allowed when ServiceControl 'AllowMessageEditing' is enabled", () => {
    test.todo(
      "EXAMPLE: ServiceControl 'AllowMessageEditing' is disabled"
      /* 
          Given a failed message is displayed in the Failed Messages list
          and the ServiceControl 'AllowMessageEditing' is disabled
          When the user sees the details of the message
          Then button for editing the message is not shown
        */
    );

    test.todo(
      "EXAMPLE: ServiceControl 'AllowMessageEditing' is enabled"
      /* 
            Given a failed message is displayed in the Failed Messages list
            and the ServiceControl 'AllowMessageEditing' is enabled
            When the user sees the details of the message
            Then button for editing the message is shown
            */
    );
  });

  describe("RULE: Only messages with with a content-type that is editable text should be allowed to be edited", () => {
    [{ contentType: "application/atom+xml" }, { contentType: "application/ld+json" }, { contentType: "application/vnd.masstransit+json" }].forEach(({ contentType }) => {
      test(`EXAMPLE: Editing a message with "${contentType}" content-type`, async ({ driver }) => {
        // Given a failed message is displayed in the Failed Messages list
        // And the message has a content-type of "${contentType}"
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.enableEditAndRetry);

        await driver.setUp(
          precondition.hasFailedMessage({
            withGroupId: "81dca64e-76fc-e1c3-11a2-3069f51c58c8",
            withMessageId: "40134401-bab9-41aa-9acb-b19c0066f22d",
            withContentType: contentType,
            withBody: { Index: 0, Data: "" },
          })
        );

        //When the user opens the message editor
        await driver.goTo("messages/81dca64e-76fc-e1c3-11a2-3069f51c58c8");
        await openEditAndRetryEditor();
        const messageEditor = await getEditAndRetryEditor();
        await messageEditor.switchToMessageBodyTab();

        //Then The message body should be editable
        expect(messageEditor.bodyFieldIsDisabled()).toBeFalsy();
      });
    });

    test(`EXAMPLE: Editing a message with a content-type not recognized as editable text`, async ({ driver }) => {
      // Given a failed message is displayed in the Failed Messages list
      // And the message has a content-type of application/octet-stream
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.enableEditAndRetry);

      await driver.setUp(
        precondition.hasFailedMessage({
          withGroupId: "81dca64e-76fc-e1c3-11a2-3069f51c58c8",
          withMessageId: "40134401-bab9-41aa-9acb-b19c0066f22d",
          withContentType: "application/octet-stream",
          withBody: { Index: 0, Data: "" },
        })
      );

      //When the user opens the message editor
      await driver.goTo("messages/81dca64e-76fc-e1c3-11a2-3069f51c58c8");
      await openEditAndRetryEditor();
      const messageEditor = await getEditAndRetryEditor();
      await messageEditor.switchToMessageBodyTab();

      //Then The message body should NOT be editable
      expect(messageEditor.bodyFieldIsDisabled()).toBeTruthy();
      expect(
        messageEditor.hasWarningMatchingText(/message body cannot be edited because content type "application\/octet-stream" is not supported\. only messages with content types "application\/json" and "text\/xml" can be edited\./i)
      ).toBeTruthy();
    });
  });
});
