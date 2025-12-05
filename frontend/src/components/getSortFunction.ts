import { type GroupPropertyType, SortDirection } from "@/resources/SortOptions";

export default function getSortFunction<T>(selector: ((group: T) => GroupPropertyType) | undefined, dir: SortDirection) {
  if (!selector) {
    return () => 0;
  }
  const sortFunc = (firstElement: T, secondElement: T) => {
    const x = selector(firstElement);
    const y = selector(secondElement);
    if (x > y) {
      return 1;
    } else if (x < y) {
      return -1;
    }
    return 0;
  };

  return dir === SortDirection.Ascending ? sortFunc : (firstElement: T, secondElement: T) => -sortFunc(firstElement, secondElement);
}
