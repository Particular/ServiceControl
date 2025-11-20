import moment from "moment";
import type { DateRange } from "@/types/date";

export interface DateDisplayOptions {
  showLocalTime?: boolean;
  showUtcTime?: boolean;
  showRelative?: boolean;
  format?: string;
  emptyText?: string;
}

/**
 * Composable for consistent date formatting across the application
 */
export function useDateFormatter() {
  const emptyDate = "0001-01-01T00:00:00";

  /**
   * Format a date range for display
   */
  function formatDateRange(dateRange: DateRange, options: DateDisplayOptions = {}): string {
    const { emptyText = "No dates" } = options;

    if (dateRange.length === 0) return emptyText;

    const [fromDate, toDate] = dateRange;

    if (toDate && toDate > new Date()) return "Date cannot be in the future";
    if (fromDate && toDate) return `${fromDate.toLocaleString()} - ${toDate.toLocaleString()}`;
    if (fromDate) return fromDate.toLocaleString();
    return emptyText;
  }

  /**
   * Format a single date with flexible options
   */
  function formatDate(dateInput: string | Date | null, options: DateDisplayOptions = {}): string {
    const { showLocalTime = true, showUtcTime = false, showRelative = false, format = "LLLL", emptyText = "n/a" } = options;

    if (!dateInput || dateInput === emptyDate) {
      return emptyText;
    }

    const m = moment.utc(dateInput);

    if (showRelative) {
      return m.fromNow();
    }

    if (showLocalTime && showUtcTime) {
      return `${m.local().format(format)} (local)\n${m.utc().format(format)} (UTC)`;
    }

    if (showUtcTime) {
      return m.utc().format(format);
    }

    return m.local().format(format);
  }

  /**
   * Format date for tooltip display (local and UTC)
   */
  function formatDateTooltip(dateInput: string | Date | null, titleValue?: string): string {
    if (titleValue) return titleValue;
    if (!dateInput || dateInput === emptyDate) return "";

    const m = moment.utc(dateInput);
    return `${m.local().format("LLLL")} (local)\n${m.utc().format("LLLL")} (UTC)`;
  }

  /**
   * Get relative time that updates periodically
   */
  function formatRelativeTime(dateInput: string | Date | null, options: DateDisplayOptions = {}): string {
    const { emptyText = "n/a" } = options;

    if (!dateInput || dateInput === emptyDate) {
      return emptyText;
    }

    return moment.utc(dateInput).fromNow();
  }

  /**
   * Format for license expiration dates
   */
  function formatLicenseDate(dateInput: string | null): string {
    if (!dateInput) return "";
    return new Date(dateInput.replace("Z", "")).toLocaleDateString();
  }

  /**
   * Validate if a date range is valid
   */
  function isValidDateRange(dateRange: DateRange): boolean {
    // Empty range is valid
    if (dateRange.length === 0) return true;

    const [fromDate, toDate] = dateRange;

    // If we have a toDate, it must not be in the future
    if (toDate && toDate > new Date()) return false;

    // If we have a fromDate but no toDate, that's valid
    if (fromDate && !toDate) return true;

    // If we have both dates, fromDate should be before or equal to toDate
    if (fromDate && toDate) return fromDate <= toDate;

    return true;
  }

  return {
    formatDate,
    formatDateRange,
    formatDateTooltip,
    formatRelativeTime,
    formatLicenseDate,
    isValidDateRange,
    emptyDate,
  };
}
