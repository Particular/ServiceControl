import { expect, it, render, screen } from "@component-test-utils";

import NoData from "./NoData.vue";

it("should displayed assigned message", async () => {
  render(NoData, { props: { message: "No messages processed in this period of time" } });
  expect(await screen.findByText("No messages processed in this period of time")).toBeVisible();
});
