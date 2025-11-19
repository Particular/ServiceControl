import { screen } from "@testing-library/vue";

export async function endpointsDetailsTitle() {
  const title = await screen.findByRole("heading", { name: "endpoint-title" });
  return title.textContent;
}
