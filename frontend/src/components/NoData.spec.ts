import { expect, test, render, screen } from "@component-test-utils";

import NoData from "./NoData.vue";

test("EXAMPLE: A messge non empty message is assigned", async () => {
  render(NoData, { props: { message: "No messages processed in this period of time" } });
  expect(await screen.findByText("No messages processed in this period of time")).toBeVisible();
});
