import { screen, within } from "@testing-library/vue";

export async function endpointDetailsLinks() {
  const endpointList = await screen.findByRole("table", { name: "endpoint-list" });
  const detailLinks = within(endpointList).queryAllByLabelText("details-link");
  const endpointDetailLinks = detailLinks.map((item) => item.getAttribute("href"));
  return endpointDetailLinks;
}
