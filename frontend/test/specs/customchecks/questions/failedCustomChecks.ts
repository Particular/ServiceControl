import { screen } from "@testing-library/vue";
import moment from "moment";

export function customChecksMessageElement() {
  const customCheckNoDataElement = screen.queryByRole("note", { name: "customcheck-message" });
  return customCheckNoDataElement;
}
export function customChecksMessage() {
  const customCheckNoDataMessage = customChecksMessageElement();
  return customCheckNoDataMessage?.textContent?.trim();
}
export function customChecksListElement() {
  const customChecksListElement = screen.queryByRole("table", { name: "custom-check-list" });
  return customChecksListElement;
}
export async function customChecksFailedRowsList() {
  const failedCustomChecksRows = await screen.findAllByRole("row", { name: "custom-check-failed-row" });
  return failedCustomChecksRows;
}
export async function customChecksFailedReasonList() {
  const failedCustomChecksReasons = await screen.findAllByRole("note", { name: "custom-check-failed-reason" });
  return failedCustomChecksReasons;
}
export function customChecksListPaginationElement() {
  const customChecksListPaginationElement = screen.queryByRole("row", { name: "custom-check-pagination" });
  return customChecksListPaginationElement;
}
export async function customChecksReportedDateList() {
  const timeElements = await screen.getAllByRole("note", { name: "custom-check-reported-date" });

  const timeStamps = timeElements.map((el) => {
    const utcDateString = el.title.match(/(\w+day, \w+ \d+, \d+ \d+:\d+ [APM]+ \(UTC\))/);

    const finalUtcString = utcDateString ? utcDateString[0] : moment.utc().format("dddd, MMMM D, YYYY h:mm A (UTC)");

    // Step 2: Parse the UTC date using moment
    const utcDate = moment(finalUtcString, "dddd, MMMM D, YYYY h:mm A (UTC)").utc().toDate(); // Converts to UTC Date object
    return utcDate.getTime();
  });

  return timeStamps;
}
export async function customChecksDismissButtonList() {
  const dismissButtonList = await screen.findAllByRole("button", { name: "custom-check-dismiss" });
  return dismissButtonList;
}
