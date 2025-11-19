import { screen } from "@testing-library/vue";

export async function endpointMessageNames() {
  const messageName = await screen.findAllByRole("message-type-name", { name: "message-type-name" });
  return messageName.map((name) => name.textContent);
}

export async function endpointMessageTypesCount() {
  const messageTypesCount = await screen.findByLabelText("message-types-count");
  return messageTypesCount.textContent;
}
