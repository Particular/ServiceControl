/**
 * A container for data with loading states.
 * Used to track loading, error, and not found states for data fetched from APIs.
 */
export interface DataContainer<T> {
  loading?: boolean;
  failed_to_load?: boolean;
  not_found?: boolean;
  data: T;
}
