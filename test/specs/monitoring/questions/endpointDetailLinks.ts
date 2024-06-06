import { screen, within } from "@testing-library/vue";

export async function endpointDetailsLinks() {
  const endpointList = await screen.findByRole("treegrid", { name: "endpoint-list" });
  const detailLinks = await within(endpointList).queryAllByRole("details-link");
  const endpointDetailLinks = detailLinks.map((item) => item.getAttribute("href"));
  return endpointDetailLinks;
}
