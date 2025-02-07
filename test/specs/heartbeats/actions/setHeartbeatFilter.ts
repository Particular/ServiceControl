import { screen } from "@testing-library/vue";
import UserEvent from "@testing-library/user-event";

export async function setHeartbeatFilter(filterText: string) {
  const filterBox = <HTMLInputElement>await screen.findByRole("searchbox", { name: "filter by name" });

  if (filterText.length > 0) {
    await UserEvent.type(filterBox, filterText);
  } else {
    await UserEvent.clear(filterBox);
  }
}
