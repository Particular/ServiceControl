import { IconDefinition } from "@fortawesome/free-solid-svg-icons";
import type { Moment } from "moment";

export type GroupPropertyType = string | number | Date | Moment | boolean;

export default interface SortOptions<T> {
  description: string;
  iconAsc: IconDefinition;
  iconDesc: IconDefinition;
  dir?: SortDirection;
  //used for client-side sorting only
  selector?: (group: T) => GroupPropertyType;
  sort?: (firstElement: T, secondElement: T) => number;
}

export enum SortDirection {
  Ascending = "asc",
  Descending = "desc",
}
