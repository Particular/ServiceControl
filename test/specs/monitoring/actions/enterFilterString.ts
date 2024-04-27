import { fireEvent, screen } from "@testing-library/vue";

export async function enterFilterString(filterString: string) {
  var filterByNameInput = await screen.findByLabelText("filter by name");
  await fireEvent.update(filterByNameInput, filterString);
}
