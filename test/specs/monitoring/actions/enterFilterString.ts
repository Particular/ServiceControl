import { screen } from "@testing-library/vue";
import UserEvent from '@testing-library/user-event';

export async function enterFilterString(filterString: string) {
  var filterByNameInput = await screen.findByLabelText("filter by name");  
  if (filterString.length > 0) {
    await UserEvent.type(filterByNameInput, filterString);
  } else {
    await UserEvent.clear(filterByNameInput);
  }
}
