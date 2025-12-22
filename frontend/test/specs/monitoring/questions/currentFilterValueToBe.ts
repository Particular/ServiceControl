import { screen } from "@testing-library/vue";

export function currentFilterValueToBe(filterString: RegExp | string) {
  const htmlElement = screen.queryByDisplayValue(filterString);
  return htmlElement != null;
}
