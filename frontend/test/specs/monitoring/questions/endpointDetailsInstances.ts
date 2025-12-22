import { screen } from "@testing-library/vue";

export async function endpointInstanceNames() {
  const messageName = await screen.findAllByRole("instance-name", { name: "instance-name" });
  return messageName.map((name) => name.textContent);
}

export async function endpointInstancesCount() {
  const messageTypesCount = await screen.findByLabelText("instances-count");
  return messageTypesCount.textContent;
}
