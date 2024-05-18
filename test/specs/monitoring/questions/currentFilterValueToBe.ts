import { screen } from "@testing-library/vue";

export function currentFilterValueToBe(filterString: RegExp | string) {
  var htmlElement = screen.queryByDisplayValue(filterString);
  return htmlElement!=null;
}
