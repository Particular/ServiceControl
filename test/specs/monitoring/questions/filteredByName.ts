import { screen } from "@testing-library/vue";

export function filteredByName(filterString: RegExp | string) {
  return screen.queryByDisplayValue(filterString);
}
