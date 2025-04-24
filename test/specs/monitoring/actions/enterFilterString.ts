import { screen } from "@testing-library/vue";
import UserEvent from "@testing-library/user-event";

export async function enterFilterString(filterString: string) {
  const filterByNameInput = await screen.findByLabelText("Filter by name");
  if (filterString.length > 0) {
    await UserEvent.type(filterByNameInput, filterString);
  } else {
    await UserEvent.clear(filterByNameInput);
  }
}
